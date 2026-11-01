using System.Diagnostics;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Services;
using FolderSwitch.Windows.Dialogs;
using FolderSwitch.Windows.Shell;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

// Folder System/, namespace SystemServices: a FolderSwitch.Windows.System namespace would hide the
// System namespace in all of FolderSwitch.Windows.
namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// Captures the call context from the foreground window: classified with <see cref="WindowClassifier"/>
/// from a <see cref="WindowInspector.Snapshot"/>, then, for an Explorer window or a file dialog, its
/// current folder (<see cref="ExplorerWindows"/>, <see cref="FileDialogInspector"/>). A browser window gives its
/// <see cref="BrowserKind"/> and no folder: nothing is read from the browser during the capture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Budget.</b> A capture takes at most <see cref="DefaultBudget"/> (50 ms), give or take a thread
/// switch. When the folder lookup does not finish in what is left, the context keeps its kind and
/// window, with no current folder: an Explorer window stays an Explorer context. The first lookups of
/// a thread are slow (COM marshaling stubs, shell start-up): every lookup thread, the first one and
/// each one that replaces an abandoned one, runs a warm-up first, so that real lookups run warm. A
/// capture that comes while the warm-up is still running waits for it within its budget; if it is not
/// done by then, the capture goes without a folder but the thread is kept (a cold lookup sent to it
/// would overrun and have it abandoned, and its replacement would start cold again). A warm-up still
/// running after <see cref="WarmUpLimit"/> is stuck (a COM call into a hung Explorer window), and its
/// thread is abandoned as below.
/// </para>
/// <para>
/// <b>Name.</b> The context also carries the name the shell shows for the folder (<c>SIGDN_NORMALDISPLAY</c>):
/// from Explorer's own ID list; for a file dialog, only when the folder is a virtual location (not a path),
/// parsed again for it.
/// </para>
/// <para>
/// <b>Threads.</b> <see cref="CaptureForeground"/> may be called from any thread, STA or MTA; captures
/// are serialized. The classification runs on the calling thread: plain Win32 calls that never wait on
/// another window. The folder lookup (shell COM, messages to the dialog) runs on an STA thread owned by
/// the provider, which the caller waits for with the remaining budget.
/// </para>
/// <para>
/// <b>Overruns.</b> A lookup that overruns is abandoned with its thread, which ends when the lookup
/// returns. Not all of it is bounded. The messages to a dialog are: each has its own timeout, and the
/// first one that does not answer ends the lookup. A COM call into Explorer is not: it waits as long as
/// the window it reaches does not answer (a window hung on an offline share, say), and the enumeration
/// asks every Explorer window for its handle. Nor is the walk through the shell namespace that resolves
/// a display path (a network location can take long). So at most one abandoned thread is kept: while it
/// is still alive, captures skip the lookup altogether (kind and window kept, no current folder) instead
/// of starting another thread that would block the same way. Once it ends, the next capture starts a
/// new lookup thread.
/// </para>
/// <para>
/// <b>Failures.</b> Nothing is thrown: no foreground window, or any exception, gives
/// <see cref="CallContext.None"/>.
/// </para>
/// </remarks>
public sealed class CallContextProvider : ICallContextProvider, IDisposable
{
    /// <summary>The time a capture may take.</summary>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromMilliseconds(50);

    /// <summary>A warm-up still running after this long is stuck: its thread is abandoned.</summary>
    internal static readonly TimeSpan WarmUpLimit = TimeSpan.FromSeconds(5);

    private const string DialogClass = "#32770";
    private const string WorkerName = "FolderSwitch call context";

    private readonly TimeSpan _budget;
    private readonly Lock _gate = new();
    private readonly Func<CallContextKind, nint, ShellLocation?> _lookup;
    private readonly bool _warmUp;
    private LookupWorker? _worker;

    /// <summary>The worker given up on after an overrun, while it may still be running its lookup.</summary>
    private LookupWorker? _abandoned;
    private bool _disposed;

    public CallContextProvider()
        : this(DefaultBudget)
    {
    }

    /// <summary>
    /// Test seam: a provider with another time budget and, optionally, another folder lookup (run on
    /// the lookup thread in place of <see cref="ExplorerWindows"/> / <see cref="FileDialogInspector"/>;
    /// no warm-up then, and no name).
    /// </summary>
    internal CallContextProvider(TimeSpan budget, Func<CallContextKind, nint, string?>? lookup = null)
    {
        _budget = budget;
        _warmUp = lookup is null;
        _lookup = lookup is null
            ? LookUp
            : (kind, window) => lookup(kind, window) is { } folder ? new ShellLocation(folder, null) : null;
        _worker = new LookupWorker(_warmUp);
    }

    /// <inheritdoc/>
    public CallContext CaptureForeground()
    {
        try
        {
            return Capture(PInvoke.GetForegroundWindow());
        }
        catch (Exception)
        {
            return CallContext.None;
        }
    }

    /// <summary>
    /// Test seam: the context <paramref name="window"/> would give as the foreground window (zero:
    /// <see cref="CallContext.None"/>).
    /// </summary>
    internal CallContext Capture(nint window)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            lock (_gate)
            {
                if (_disposed || window == 0)
                {
                    return CallContext.None;
                }

                if (Classify(window) is not (var target, var kind, var browser))
                {
                    return CallContext.None;
                }

                // No folder to look up, so no search on the lookup thread.
                if (kind == CallContextKind.Other)
                {
                    return new CallContext(CallContextKind.Other, target, null);
                }

                if (kind == CallContextKind.Browser)
                {
                    return new CallContext(CallContextKind.Browser, target, null, null, browser);
                }

                var location = LookUpFolder(kind, target, start);
                return new CallContext(kind, target, location?.ParsingName, location?.DisplayName);
            }
        }
        catch (Exception)
        {
            return CallContext.None;
        }
    }

    /// <summary>Test seam: whether an abandoned lookup is still running, so that captures skip the lookup.</summary>
    internal bool IsLookupSuspended
    {
        get
        {
            lock (_gate)
            {
                return _abandoned?.Thread.IsAlive == true;
            }
        }
    }

    /// <summary>Stops the lookup thread. Later captures give <see cref="CallContext.None"/>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _worker?.Thread.Dispose();
            _worker = null;
        }
    }

    /// <summary>
    /// The window to classify and its kind. A dialog is judged by itself (its root owner is the
    /// application's main window). Another window that is not recognized, typically a popup of an
    /// Explorer window that took the activation (a Windows 11 menu or flyout), stands for its root
    /// owner when that owner is an Explorer window. <c>null</c> for a window that is gone. The browser is
    /// the window's, for a <see cref="CallContextKind.Browser"/> kind.
    /// </summary>
    private static (nint Window, CallContextKind Kind, BrowserKind? Browser)? Classify(nint window)
    {
        var info = WindowInspector.Snapshot(window);
        if (info.ClassName.Length == 0)
        {
            return null;
        }

        var kind = WindowClassifier.Classify(info);
        if (kind != CallContextKind.Other || string.Equals(info.ClassName, DialogClass, StringComparison.Ordinal))
        {
            return (window, kind, kind == CallContextKind.Browser ? WindowClassifier.BrowserOf(info) : null);
        }

        var owner = (nint)PInvoke.GetAncestor((HWND)window, GET_ANCESTOR_FLAGS.GA_ROOTOWNER);
        if (owner != 0 && owner != window
            && WindowClassifier.Classify(WindowInspector.Snapshot(owner)) == CallContextKind.Explorer)
        {
            return (owner, CallContextKind.Explorer, null);
        }

        return (window, kind, null);
    }

    /// <summary>
    /// The current folder and its name, or <c>null</c> if it is unknown, not read within what is left of the
    /// budget since <paramref name="start"/>, or not looked up (an abandoned lookup still running, a new
    /// lookup thread still warming up).
    /// </summary>
    private ShellLocation? LookUpFolder(CallContextKind kind, nint window, long start)
    {
        if (_abandoned is not null)
        {
            if (_abandoned.Thread.IsAlive)
            {
                return null;
            }

            _abandoned = null;
        }

        var worker = _worker ??= new LookupWorker(_warmUp);
        if (!worker.WaitUntilWarm(Remaining(start)))
        {
            if (Stopwatch.GetElapsedTime(worker.Created) > WarmUpLimit)
            {
                Abandon();
            }

            return null;
        }

        var remaining = Remaining(start);
        if (remaining <= TimeSpan.Zero)
        {
            return null;
        }

        if (worker.Thread.TryRun(() => _lookup(kind, window), remaining, out var location))
        {
            return location;
        }

        Abandon();
        return null;
    }

    private TimeSpan Remaining(long start) => _budget - Stopwatch.GetElapsedTime(start);

    /// <summary>Under the gate: the worker overran; its thread is left to finish on its own, and no other starts until it has.</summary>
    private void Abandon()
    {
        if (_worker is not { } worker)
        {
            return;
        }

        worker.Thread.Dispose();
        _abandoned = worker;
        _worker = null;
    }

    private static ShellLocation? LookUp(CallContextKind kind, nint window)
    {
        if (kind == CallContextKind.Explorer)
        {
            return ExplorerWindows.CurrentLocation(window);
        }

        // A dialog gives a path or a parsing name: the name the shell shows is only worth asking for a virtual location.
        return FileDialogInspector.CurrentFolder(window) is { } folder
            ? new ShellLocation(folder, Path.IsPathFullyQualified(folder) ? null : ShellNative.DisplayNameOf(folder))
            : null;
    }

    /// <summary>
    /// Runs the lookup code paths once, results ignored: the COM marshaling stubs get generated, the
    /// shell's proxies and the desktop folder get loaded. Read-only.
    /// </summary>
    private static void WarmUp()
    {
        var browsers = ExplorerWindows.Enumerate();
        try
        {
            _ = browsers.FirstOrDefault()?.CurrentFolder();
        }
        finally
        {
            browsers.ForEach(b => b.Dispose());
        }

        // "|" cannot be a name: parsed, then compared with the desktop's children, then not found.
        _ = ShellNamespace.ResolveDisplayPath(["|"]);
        _ = FileDialogInspector.StripAddressLabel("Address: C:\\");
    }

    /// <summary>A lookup thread and its warm-up, queued first on it (none for a test's own lookup).</summary>
    private sealed class LookupWorker
    {
        private readonly TaskCompletionSource _warm = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LookupWorker(bool warmUp)
        {
            Thread = new StaWorker(WorkerName);
            Created = Stopwatch.GetTimestamp();
            if (!warmUp)
            {
                _warm.SetResult();
                return;
            }

            Thread.Post(() =>
            {
                try
                {
                    WarmUp();
                }
                finally
                {
                    _warm.TrySetResult();
                }
            });
        }

        public StaWorker Thread { get; }

        /// <summary>When it was created (a <see cref="Stopwatch"/> timestamp).</summary>
        public long Created { get; }

        /// <summary>Whether the warm-up is over, waiting for it at most <paramref name="timeout"/>.</summary>
        public bool WaitUntilWarm(TimeSpan timeout) =>
            _warm.Task.IsCompleted || (timeout > TimeSpan.Zero && _warm.Task.Wait(timeout));
    }
}
