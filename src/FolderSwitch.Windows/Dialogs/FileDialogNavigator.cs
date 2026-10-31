using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Services;
using FolderSwitch.Windows.Shell;
using FolderSwitch.Windows.SystemServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace FolderSwitch.Windows.Dialogs;

/// <summary>
/// Moves a file dialog of another process to a folder, the way a user would, for each kind of dialog
/// that <see cref="WindowClassifier"/> calls a <see cref="CallContextKind.FileDialog"/>:
/// <list type="bullet">
/// <item>Common Item Dialog (Open, Save As, folder picker) and legacy Explorer-style Open / Save As:
/// the folder is typed in the file name box (<c>WM_SETTEXT</c>) and validated (<c>WM_COMMAND</c>
/// <c>IDOK</c>, what a click on the OK button sends), which moves the dialog to a folder instead of
/// validating a file. The box gets its text back afterwards.</item>
/// <item>Legacy folder picker (<c>SHBrowseForFolder</c>, both styles): <c>BFFM_SETSELECTIONW</c> with
/// the path, in a block of the dialog's process (<see cref="RemoteMemory"/>).</item>
/// </list>
/// Each move is verified with <see cref="FileDialogInspector.CurrentFolder"/>, and tried a second time
/// after <see cref="RetryDelay"/> when it may be.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only folders, only file dialogs.</b> Nothing is sent to a window that is not a file dialog. The
/// location must parse (<c>SHParseDisplayName</c>) as a folder that is not a file
/// (<c>SFGAO_FOLDER</c> without <c>SFGAO_STREAM</c>, so not a .zip either) and that is, or contains,
/// file-system folders (<c>SFGAO_FILESYSTEM</c> or <c>SFGAO_FILESYSANCESTOR</c>): This PC, Libraries
/// and Network are, the Recycle Bin and Control Panel are not. Measured: an Open dialog validates what
/// it cannot browse as its result, and closes, for a file path (even with a trailing separator) and for
/// the Recycle Bin; a Save As dialog shows an error box. A location with a file-system path is typed as
/// that path with a trailing separator; one without (This PC: <c>shell:MyComputerFolder</c>,
/// <c>::{CLSID}</c>) as given, which every kind of dialog understands.
/// </para>
/// <para>
/// <b>The file name box.</b> Its text is read first; the folder replaces it, and is read back just
/// before OK, since OK validates whatever the box holds. After a move, the dialogs put a text of their
/// own back in the box, the file name they were given (not what the user typed since) or, for the
/// folder picker, the name of the new folder: that is waited for, then an Open / Save As box gets its
/// original text back, while the folder picker keeps its own (the original text would name a
/// subfolder of the new folder). OK is sent with <c>SendMessageTimeout</c>, not posted: its return says
/// the dialog has handled it (a posted OK could still be waiting in the queue when the text is put back,
/// and would then validate the file name). A second OK is sent only once the dialog has visibly finished
/// with the first one (it put its own text back without showing the folder), since that text,
/// validated, would be a file to open or save; a first OK whose outcome does not show yet (a slow move
/// to a network location) is waited for, not repeated. An OK that times out ends the attempts, but is
/// waited for in the same way: until the dialog answers again and shows its outcome, or until the end.
/// If it still shows nothing by then, the folder typed stays in the box (<see cref="FileNameBoxRule"/>):
/// a dialog that defers its OK through a posted message would take a text sent meanwhile (sent messages
/// come first) and validate it, while the folder, validated late, only moves the dialog, which then puts
/// its own text back. A text put back after a text message that timed out is handled after it (sent
/// messages are handled in order).
/// </para>
/// <para>
/// <b>Bounds.</b> Every message has a timeout (<see cref="MessageTimeoutMs"/>). The first message is
/// also the check that the dialog answers: a dialog that does not answer is left as it was. A message
/// that times out ends the move (a block of the folder picker is then left to the dialog, see
/// <see cref="RemoteMemory"/>; after an OK, only the waiting described above follows), and
/// <c>false</c> is returned unless that waiting shows the folder.
/// </para>
/// <para>
/// <b>Threads.</b> Any thread, the UI thread included; calls are serialized. The work runs on an STA
/// thread owned by the navigator (shell parsing, <see cref="FileDialogInspector"/>), which the caller
/// waits for at most <see cref="DefaultBudget"/>: some of it is not bounded, namely the parsing of a
/// network path and the walk through the shell namespace that reads a folder picker's tree (a network
/// location can take long). As in <c>CallContextProvider</c>, work that overruns is abandoned with its
/// thread and returns <c>false</c>; it makes no further move once given up, but still puts the text
/// back when the rule above allows it. While that thread lives, <see cref="Navigate"/> returns
/// <c>false</c> at once.
/// </para>
/// </remarks>
public sealed class FileDialogNavigator : IFileDialogNavigator, IDisposable
{
    /// <summary>The time <see cref="Navigate"/> may take.</summary>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromMilliseconds(1500);

    /// <summary>Timeout of each message sent to the dialog.</summary>
    internal const uint MessageTimeoutMs = 500;

    /// <summary>The pause before the second and last attempt.</summary>
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>How long the first attempt is given to show the folder.</summary>
    private static readonly TimeSpan FirstAttemptTimeout = TimeSpan.FromMilliseconds(600);

    /// <summary>When the attempts stop waiting, from the start of the work; the rest of the budget is for the text.</summary>
    private static readonly TimeSpan AttemptsEnd = TimeSpan.FromMilliseconds(1150);

    /// <summary>How long a dialog that has moved is given to put a text of its own back in the box.</summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    private const string WorkerName = "FolderSwitch file dialog navigation";

    private const string BreadcrumbParentClass = "Breadcrumb Parent";
    private const string ShellViewClass = "SHELLDLL_DefView";
    private const string ComboBoxExClass = "ComboBoxEx32";
    private const string ComboBoxClass = "ComboBox";
    private const string EditClass = "Edit";
    private const string DirectUiHostClass = "DUIViewWndClassName";
    private const string DirectUiClass = "DirectUIHWND";
    private const string FloatNotifySinkClass = "FloatNotifySink";

    private const int IdOk = 1;

    /// <summary>The file name combo box, <c>cmb13</c>: Open (Common Item Dialog), legacy Open / Save As.</summary>
    private const int FileNameComboId = 0x47C;

    /// <summary>The plain file name edit, <c>edt1</c>: the Common Item Dialog's folder picker, older templates.</summary>
    private const int FileNameEditId = 0x480;

    /// <summary><c>BFFM_SETSELECTIONW</c> (<c>WM_USER + 103</c>), the value of <c>CDM_GETFOLDERIDLIST</c> too.</summary>
    private const uint BffmSetSelectionW = 0x0400 + 103;

    /// <summary>A file name box text this long or longer is not touched: it could not be put back whole.</summary>
    private const int MaxTextChars = 32768;

    private const int MaxSinks = 32;

    private readonly TimeSpan _budget;
    private readonly Lock _gate = new();
    private StaWorker? _worker;

    /// <summary>The worker given up on after an overrun, while it may still be running its work.</summary>
    private StaWorker? _abandoned;
    private bool _disposed;

    public FileDialogNavigator()
        : this(DefaultBudget)
    {
    }

    /// <summary>Test seam: another time budget.</summary>
    internal FileDialogNavigator(TimeSpan budget)
    {
        _budget = budget;
    }

    /// <inheritdoc/>
    /// <returns>
    /// <c>true</c> once the dialog shows <paramref name="folder"/> (at once if it already did);
    /// <c>false</c> if the window is not a file dialog, the location is not a folder such a dialog can
    /// show, the dialog does not answer, or the move could not be verified in time. Never throws.
    /// </returns>
    public bool Navigate(nint dialog, string folder)
    {
        try
        {
            return dialog != 0
                && !string.IsNullOrWhiteSpace(folder)
                && RunBounded(givenUp => NavigateCore(dialog, folder.Trim(), givenUp));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Stops the worker thread. Later navigations return <c>false</c>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _worker?.Dispose();
            _worker = null;
        }
    }

    /// <summary>Test seam: whether an abandoned piece of work is still running, so that navigations fail at once.</summary>
    internal bool IsSuspended
    {
        get
        {
            lock (_gate)
            {
                return _abandoned?.IsAlive == true;
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> on the worker thread within the budget. <c>false</c> if it returned
    /// <c>false</c>, overran (the worker is then abandoned), or did not run (disposed, or an abandoned
    /// worker still running). The token given to the work is cancelled on an overrun: the caller has
    /// given up, so the work must not make any further move. Internal for tests.
    /// </summary>
    internal bool RunBounded(Func<CancellationToken, bool> work)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            if (_abandoned is not null)
            {
                if (_abandoned.IsAlive)
                {
                    return false;
                }

                _abandoned = null;
            }

            _worker ??= new StaWorker(WorkerName);

            // The token stays readable after the source is disposed, by the work still running.
            using var givenUp = new CancellationTokenSource();
            var token = givenUp.Token;
            if (_worker.TryRun(() => work(token), _budget, out var done))
            {
                return done;
            }

            // Overran: that thread is left to finish on its own, and no other starts until it has.
            givenUp.Cancel();
            _worker.Dispose();
            _abandoned = _worker;
            _worker = null;
            return false;
        }
    }

    // ---- On the worker thread ----

    private static bool NavigateCore(nint dialog, string folder, CancellationToken givenUp)
    {
        var start = Stopwatch.GetTimestamp();
        if (!PInvoke.IsWindow((HWND)dialog))
        {
            return false;
        }

        var info = WindowInspector.Snapshot(dialog);
        if (WindowClassifier.Classify(info) != CallContextKind.FileDialog || Target.Parse(folder) is not { } target)
        {
            return false;
        }

        // The kinds are told apart in the order FileDialogInspector.CurrentFolder reads them.
        if (WindowInspector.FindDescendant(dialog, BreadcrumbParentClass) != 0)
        {
            return ThroughFileNameBox(dialog, commonItemDialog: true, target, start, givenUp);
        }

        if (WindowClassifier.IsLegacyFolderPicker(info))
        {
            return ThroughTreeSelection(dialog, target, start, givenUp);
        }

        return WindowInspector.FindChild(dialog, ShellViewClass) != 0
            && ThroughFileNameBox(dialog, commonItemDialog: false, target, start, givenUp);
    }

    /// <summary>Open, Save As and the Common Item Dialog's folder picker: the folder typed in the file name box, then OK.</summary>
    private static bool ThroughFileNameBox(nint dialog, bool commonItemDialog, Target target, long start, CancellationToken givenUp)
    {
        var box = FileNameBox(dialog, commonItemDialog, out var isFolderNameBox);

        // The first message, which also checks that the dialog answers: nothing changes in one that does not.
        if (box == 0 || WindowMessages.GetText(box, MaxTextChars, MessageTimeoutMs) is not { } original)
        {
            return false;
        }

        if (IsAt(dialog, target))
        {
            return true;
        }

        var typed = target.TypedForm;
        var end = start + Ticks(AttemptsEnd);
        var outcome = DialogMove.Pending;
        var lastOk = DialogOk.NotSent;
        var touched = false;
        try
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                if (attempt == 2)
                {
                    Thread.Sleep(RetryDelay);
                }

                if (givenUp.IsCancellationRequested || !IsSameKind(dialog, legacyFolderPicker: false))
                {
                    break;
                }

                touched = true;
                lastOk = TypeAndValidate(dialog, box, typed, givenUp);
                if (lastOk == DialogOk.NotSent)
                {
                    break;
                }

                // An OK that timed out may not have been handled yet: the dialog could still read the
                // box after a text sent now (sent messages come before posted ones, should it defer
                // its OK), and validate that text. So it is waited for like a pending OK, until the
                // dialog shows its outcome or the end, then never repeated; if it still shows nothing
                // then, the box keeps the folder (see FileNameBoxRule).
                if (lastOk == DialogOk.Unanswered)
                {
                    outcome = WaitForFolder(dialog, target, box, typed, end);
                    break;
                }

                // A first OK whose outcome does not show yet (the box still holds the folder: the dialog
                // may still be moving, to a network location say) is waited for until the end, never
                // repeated. A second one only follows an OK the dialog is visibly done with.
                outcome = WaitForFolder(dialog, target, box, typed, attempt == 1 ? Math.Min(Stopwatch.GetTimestamp() + Ticks(FirstAttemptTimeout), end) : end);
                if (outcome == DialogMove.Pending && attempt == 1)
                {
                    outcome = WaitForFolder(dialog, target, box, typed, end);
                }

                if (outcome != DialogMove.Settled || Stopwatch.GetTimestamp() + Ticks(RetryDelay) >= end)
                {
                    break;
                }
            }
        }
        finally
        {
            if (touched && FileNameBoxRule.MayPutTextBack(lastOk, outcome))
            {
                PutTextBack(box, typed, original, isFolderNameBox, waitForDialog: outcome == DialogMove.Reached);
            }
        }

        return outcome == DialogMove.Reached;
    }

    /// <summary>
    /// Types <paramref name="typed"/> in the box, reads it back, then sends OK, as a click on the OK
    /// button does. OK is not sent if a message before it failed or timed out, or if the box does not
    /// hold exactly the folder just before OK (OK validates whatever the box holds).
    /// </summary>
    private static DialogOk TypeAndValidate(nint dialog, nint box, string typed, CancellationToken givenUp)
    {
        if (!WindowMessages.SetText(box, typed, MessageTimeoutMs)
            || WindowMessages.GetText(box, MaxTextChars, MessageTimeoutMs) != typed
            || givenUp.IsCancellationRequested)
        {
            return DialogOk.NotSent;
        }

        return WindowMessages.Send(dialog, PInvoke.WM_COMMAND, IdOk, OkButton(dialog), MessageTimeoutMs) is null
            ? DialogOk.Unanswered
            : DialogOk.Handled;
    }

    /// <summary>The legacy folder picker: its tree selection moved with <c>BFFM_SETSELECTIONW</c>.</summary>
    private static bool ThroughTreeSelection(nint dialog, Target target, long start, CancellationToken givenUp)
    {
        // A dialog that does not answer is left alone: a selection message still queued would move it later.
        if (WindowMessages.Send(dialog, PInvoke.WM_NULL, 0, 0, MessageTimeoutMs) is null)
        {
            return false;
        }

        if (IsAt(dialog, target))
        {
            return true;
        }

        var path = Encoding.Unicode.GetBytes(target.PickerForm + "\0");
        using var remote = RemoteMemory.Open(dialog, path.Length);
        if (remote is null || !remote.Write(0, path))
        {
            return false;
        }

        var end = start + Ticks(AttemptsEnd);
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            if (attempt == 2)
            {
                // No second attempt that would start past the end, and so past the budget.
                if (Stopwatch.GetTimestamp() + Ticks(RetryDelay) >= end)
                {
                    return false;
                }

                Thread.Sleep(RetryDelay);
            }

            // BFFM_SETSELECTIONW has the value of CDM_GETFOLDERIDLIST: the kind is checked again before each one.
            if (givenUp.IsCancellationRequested || !IsSameKind(dialog, legacyFolderPicker: true))
            {
                return false;
            }

            // wParam TRUE: lParam points to a path, not to an ID list. A message that does not complete
            // leaves the block to the dialog (abandoned, never freed), and ends the move.
            if (remote.Send(dialog, BffmSetSelectionW, 1, 0, MessageTimeoutMs) is null)
            {
                return false;
            }

            // Selecting again is harmless: the retry needs no precaution here.
            var until = attempt == 1 ? Math.Min(Stopwatch.GetTimestamp() + Ticks(FirstAttemptTimeout), end) : end;
            if (WaitForFolder(dialog, target, 0, "", until) == DialogMove.Reached)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Polls the dialog until it shows <paramref name="target"/> or closes, or until
    /// <paramref name="until"/> (a <see cref="Stopwatch"/> timestamp).
    /// </summary>
    private static DialogMove WaitForFolder(nint dialog, Target target, nint box, string typed, long until)
    {
        var settled = false;
        while (true)
        {
            if (!PInvoke.IsWindow((HWND)dialog))
            {
                return DialogMove.Gone;
            }

            if (IsAt(dialog, target))
            {
                return DialogMove.Reached;
            }

            settled |= box != 0 && WindowMessages.GetText(box, MaxTextChars, MessageTimeoutMs) is { } text && text != typed;
            if (Stopwatch.GetTimestamp() >= until)
            {
                return settled ? DialogMove.Settled : DialogMove.Pending;
            }

            Thread.Sleep(PollInterval);
        }
    }

    /// <summary>
    /// Leaves the box with its original text, or for the folder picker with the dialog's own text, as
    /// <see cref="FileNameBoxRule.NeedsOriginal"/> says. After a move (<paramref name="waitForDialog"/>),
    /// the dialog's own text is waited for first, so that it does not land after the original one.
    /// Called only when <see cref="FileNameBoxRule.MayPutTextBack"/> allows it.
    /// </summary>
    private static void PutTextBack(nint box, string typed, string original, bool isFolderNameBox, bool waitForDialog)
    {
        var current = WindowMessages.GetText(box, MaxTextChars, MessageTimeoutMs);
        if (waitForDialog)
        {
            var until = Stopwatch.GetTimestamp() + Ticks(SettleTimeout);
            while (current == typed && Stopwatch.GetTimestamp() < until)
            {
                Thread.Sleep(PollInterval);
                current = WindowMessages.GetText(box, MaxTextChars, MessageTimeoutMs);
            }
        }

        // Unknown (null): put back too.
        if (FileNameBoxRule.NeedsOriginal(current, typed, original, isFolderNameBox))
        {
            _ = WindowMessages.SetText(box, original, MessageTimeoutMs);
        }
    }

    /// <summary>
    /// The edit control of the file name box, or zero. Measured on Windows 11 (build 26300):
    /// <list type="bullet">
    /// <item>Open (Common Item Dialog), legacy Open / Save As: <c>ComboBoxEx32</c> 0x47C &gt;
    /// <c>ComboBox</c> 0x47C &gt; <c>Edit</c> 0x47C, children of the dialog;</item>
    /// <item>Common Item Dialog folder picker: <c>Edit</c> 0x480, child of the dialog
    /// (<paramref name="isFolderNameBox"/>);</item>
    /// <item>Save As (Common Item Dialog): no 0x47C; <c>DUIViewWndClassName</c> &gt; <c>DirectUIHWND</c>
    /// &gt; <c>FloatNotifySink</c> &gt; <c>ComboBox</c> &gt; <c>Edit</c> 0x3E9, the first sink holding an
    /// editable combo box (the file type one is a drop-down list, without an edit).</item>
    /// </list>
    /// Hidden controls are skipped. No message is sent.
    /// </summary>
    private static nint FileNameBox(nint dialog, bool commonItemDialog, out bool isFolderNameBox)
    {
        isFolderNameBox = false;

        var combo = (nint)PInvoke.GetDlgItem((HWND)dialog, FileNameComboId);
        if (combo != 0 && PInvoke.IsWindowVisible((HWND)combo))
        {
            var edit = WindowInspector.ClassName(combo) switch
            {
                ComboBoxExClass => WindowInspector.FindChild(WindowInspector.FindChild(combo, ComboBoxClass), EditClass),
                ComboBoxClass => WindowInspector.FindChild(combo, EditClass),
                _ => 0,
            };
            if (edit != 0)
            {
                return edit;
            }
        }

        var plain = (nint)PInvoke.GetDlgItem((HWND)dialog, FileNameEditId);
        if (plain != 0 && PInvoke.IsWindowVisible((HWND)plain) && WindowInspector.ClassName(plain) == EditClass)
        {
            isFolderNameBox = commonItemDialog;
            return plain;
        }

        if (!commonItemDialog)
        {
            return 0;
        }

        var directUi = WindowInspector.FindChild(WindowInspector.FindChild(dialog, DirectUiHostClass), DirectUiClass);
        var sink = HWND.Null;
        for (var i = 0; i < MaxSinks && directUi != 0; i++)
        {
            sink = PInvoke.FindWindowEx((HWND)directUi, sink, FloatNotifySinkClass, null);
            if (sink.IsNull)
            {
                break;
            }

            var edit = WindowInspector.FindChild(WindowInspector.FindChild((nint)sink, ComboBoxClass), EditClass);
            if (edit != 0 && PInvoke.IsWindowVisible((HWND)edit))
            {
                return edit;
            }
        }

        return 0;
    }

    private static nint OkButton(nint dialog) => (nint)PInvoke.GetDlgItem((HWND)dialog, IdOk);

    /// <summary>Whether the window is still a file dialog of that kind (it could have closed meanwhile).</summary>
    private static bool IsSameKind(nint dialog, bool legacyFolderPicker)
    {
        var info = WindowInspector.Snapshot(dialog);
        return WindowClassifier.Classify(info) == CallContextKind.FileDialog
            && WindowClassifier.IsLegacyFolderPicker(info) == legacyFolderPicker;
    }

    private static bool IsAt(nint dialog, Target target) => target.Matches(FileDialogInspector.CurrentFolder(dialog));

    private static long Ticks(TimeSpan span) => (long)(span.TotalSeconds * Stopwatch.Frequency);

    /// <summary>A location a file dialog can show, parsed.</summary>
    /// <param name="Name">The location as given: a path, a <c>shell:</c> or <c>::{CLSID}</c> form.</param>
    /// <param name="ParsingName">Its parsing name, the form <see cref="FileDialogInspector.CurrentFolder"/> gives.</param>
    /// <param name="FileSystemPath">Its file-system path, or <c>null</c> (This PC, Libraries...).</param>
    private sealed record Target(string Name, string ParsingName, string? FileSystemPath)
    {
        private const uint Queried =
            ShellNative.SfgaoFolder | ShellNative.SfgaoFileSystem | ShellNative.SfgaoFileSysAncestor | ShellNative.SfgaoStream;

        /// <summary>What goes in the file name box: the path with a trailing separator, or the shell form.</summary>
        public string TypedForm => FileSystemPath is { } path ? (path.EndsWith('\\') ? path : path + '\\') : Name;

        /// <summary>What goes to <c>BFFM_SETSELECTIONW</c>: the path, or the shell form.</summary>
        public string PickerForm => FileSystemPath ?? Name;

        /// <summary>Whether a dialog's current folder is this one: case and trailing separator aside, in parsing form or as given.</summary>
        public bool Matches(string? folder) => folder is not null && (Same(folder, ParsingName) || Same(folder, Name));

        /// <summary>The location, if it parses as a folder a file dialog can show (see the class remarks); <c>null</c> otherwise.</summary>
        public static Target? Parse(string name)
        {
            var hr = ShellNative.SHParseDisplayName(name, 0, out var pidl, Queried, out var attributes);
            if (pidl == 0)
            {
                return null;
            }

            try
            {
                var showable = (attributes & ShellNative.SfgaoFolder) != 0
                    && (attributes & ShellNative.SfgaoStream) == 0
                    && (attributes & (ShellNative.SfgaoFileSystem | ShellNative.SfgaoFileSysAncestor)) != 0;
                return hr == ShellNative.S_OK && showable && ShellNative.ParsingName(pidl) is { } parsingName
                    ? new Target(name, parsingName, ShellNative.FileSystemPath(pidl))
                    : null;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pidl);
            }
        }

        private static bool Same(string a, string b) =>
            string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }
}
