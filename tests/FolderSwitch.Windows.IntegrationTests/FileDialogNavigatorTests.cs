// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FolderSwitch.Windows.Dialogs;
using FolderSwitch.Windows.IntegrationTests.Support;
using Xunit.Abstractions;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Runs on the real desktop session: opens DialogHost dialogs (and a message box) on temporary
/// folders, moves them with the navigator, called from an STA thread as the UI thread would, and
/// checks the folder the dialog reports (DialogHost's <c>FOLDER</c> lines, which come from the dialog
/// itself) and what <see cref="FileDialogInspector"/> reads. Every window opened is closed, also when
/// an assertion fails. No keystroke is sent: the tests read and set the file name box with
/// <c>WM_GETTEXT</c> / <c>WM_SETTEXT</c>, found by its measured place in each kind of dialog.
/// </summary>
[Trait("Category", "Desktop")]
[Collection(ForegroundCollection.Name)]
public sealed class FileDialogNavigatorTests(ITestOutputHelper output) : IDisposable
{
    private const string ThisPc = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    private const string Unc = @"\\localhost\C$";
    private const string FileName = "rapport.txt";

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReportTimeout = TimeSpan.FromSeconds(5);

    /// <summary>What a call may take beyond the budget: the STA thread of the test, thread switches.</summary>
    private static readonly TimeSpan Margin = TimeSpan.FromMilliseconds(150);

    private static readonly Lazy<bool> UncReachable = new(() =>
    {
        var check = Task.Run(() => Directory.Exists(Unc));
        return check.Wait(TimeSpan.FromSeconds(5)) && check.Result;
    });

    private readonly FileDialogNavigator _sut = new();
    private readonly TempFolder _a = new();
    private readonly TempFolder _b = new();

    public void Dispose()
    {
        _sut.Dispose();
        _a.Dispose();
        _b.Dispose();
    }

    // ---- Brief tests ----

    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("folder")]
    [InlineData("legacy-open")]
    [InlineData("legacy-save")]
    [InlineData("legacy-folder")]
    [InlineData("legacy-folder-classic")]
    public void Navigates_every_dialog_kind(string mode)
    {
        using var host = Open(mode, _a.Path);
        var before = BoxText(host.Dialog, mode);

        var (moved, elapsed) = Navigate(host.Dialog, _b.Path);
        output.WriteLine($"{mode}: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms; box '{before}' -> '{BoxText(host.Dialog, mode)}'");

        Assert.True(moved, host.Transcript);
        AssertReported(host, _b.Path);
        AssertShows(host, _b.Path);
        Assert.True(elapsed < FileDialogNavigator.DefaultBudget + Margin, $"{elapsed.TotalMilliseconds:F0} ms");
        Assert.False(host.HasExited, "The dialog closed: " + host.Transcript);
        AssertBoxAfterMove(host.Dialog, mode, before, _b.Path);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("legacy-save")]
    [InlineData("open")]
    [InlineData("legacy-open")]
    public void Restores_filename_text(string mode)
    {
        using var host = Open(mode, _a.Path);
        Assert.Equal(FileName, BoxText(host.Dialog, mode));

        Assert.True(Navigate(host.Dialog, _b.Path).Moved, host.Transcript);
        AssertReported(host, _b.Path);

        Assert.Equal(FileName, BoxText(host.Dialog, mode));

        // Still there once the dialog has settled.
        Thread.Sleep(500);
        Assert.Equal(FileName, BoxText(host.Dialog, mode));
        Assert.False(host.HasExited, "The dialog closed: " + host.Transcript);
    }

    /// <summary>
    /// After a move, the dialog itself puts back the file name it was given, not what the user typed
    /// since: the navigator puts back what the box held.
    /// </summary>
    [Theory]
    [InlineData("save")]
    [InlineData("legacy-save")]
    [InlineData("open")]
    public void Restores_the_text_typed_since_the_dialog_opened(string mode)
    {
        using var host = Open(mode, _a.Path);
        SetBoxText(host.Dialog, mode, "brouillon.txt");

        Assert.True(Navigate(host.Dialog, _b.Path).Moved, host.Transcript);
        AssertReported(host, _b.Path);

        Thread.Sleep(300);
        Assert.Equal("brouillon.txt", BoxText(host.Dialog, mode));
    }

    [Fact]
    public void Refuses_non_file_dialog()
    {
        using var box = MessageBoxScope.Show();
        var caption = Win32.Text(box.Window);
        var message = Win32.Text(Win32.GetDlgItem(box.Window, Win32.MessageBoxTextId));
        Assert.False(string.IsNullOrEmpty(message));

        var (moved, elapsed) = Navigate(box.Window, _b.Path);
        output.WriteLine($"message box: Navigate {moved} in {elapsed.TotalMilliseconds:F1} ms");

        Assert.False(moved);

        // Nothing sent: neither OK (the box would have closed) nor a text.
        Thread.Sleep(200);
        Assert.True(Win32.IsWindow(box.Window), "The message box closed.");
        Assert.Equal(caption, Win32.Text(box.Window));
        Assert.Equal(message, Win32.Text(Win32.GetDlgItem(box.Window, Win32.MessageBoxTextId)));
    }

    [Theory]
    [InlineData("open")]
    [InlineData("legacy-open")]
    [InlineData("legacy-folder")]
    public void Hung_dialog_returns_false_within_budget(string mode)
    {
        using var host = Open(mode, _a.Path);
        var before = BoxText(host.Dialog, mode);
        host.Send("hang 1500");
        Assert.NotNull(host.WaitForLine(l => l == "HUNG", TimeSpan.FromSeconds(5)));

        var (moved, elapsed) = Navigate(host.Dialog, _b.Path);
        output.WriteLine($"{mode} hung: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.False(moved);
        Assert.True(elapsed < FileDialogNavigator.DefaultBudget + Margin, $"{elapsed.TotalMilliseconds:F0} ms");

        // Awake again: unharmed, and left as it was (a dialog that does not answer is not touched).
        Assert.NotNull(host.WaitForLine(l => l == "AWAKE", TimeSpan.FromSeconds(5)));
        Thread.Sleep(500);
        Assert.False(host.HasExited, "DialogHost ended: " + host.Transcript);
        AssertShows(host, _a.Path);
        Assert.Equal(before, BoxText(host.Dialog, mode));
        Assert.Equal(0, host.QuitAndWait());
    }

    [Theory]
    [InlineData("open", "shell:MyComputerFolder")]
    [InlineData("save", "shell:MyComputerFolder")]
    [InlineData("folder", "shell:MyComputerFolder")]
    [InlineData("legacy-open", "shell:MyComputerFolder")]
    [InlineData("legacy-save", "shell:MyComputerFolder")]
    [InlineData("legacy-folder", "shell:MyComputerFolder")]
    [InlineData("legacy-folder-classic", "shell:MyComputerFolder")]
    [InlineData("open", ThisPc)]
    [InlineData("legacy-save", ThisPc)]
    [InlineData("legacy-folder-classic", ThisPc)]
    [InlineData("open", "shell:Personal")]
    [InlineData("save", "shell:Personal")]
    [InlineData("folder", "shell:Personal")]
    [InlineData("legacy-open", "shell:Personal")]
    [InlineData("legacy-save", "shell:Personal")]
    [InlineData("legacy-folder", "shell:Personal")]
    [InlineData("legacy-folder-classic", "shell:Personal")]
    [InlineData("legacy-folder", "shell:startup")]
    [InlineData("legacy-folder-classic", "shell:startup")]
    public void Navigates_to_shell_form(string mode, string location)
    {
        // This PC has no file-system path: typed as given. Documents and Startup have one: typed as
        // that path. A picker's tree shows Startup under localized names ("Menu Démarrer > Programmes >
        // Démarrage" in French), next to a hidden compatibility junction named like one of them.
        var expected = location switch
        {
            "shell:Personal" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "shell:startup" => Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            _ => ThisPc,
        };
        using var host = Open(mode, _a.Path);
        var before = BoxText(host.Dialog, mode);

        var (moved, elapsed) = Navigate(host.Dialog, location);
        output.WriteLine($"{mode} -> {location}: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.True(moved, host.Transcript);
        AssertReported(host, expected);
        AssertShows(host, expected);
        AssertBoxAfterMove(host.Dialog, mode, before, expected);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("folder")]
    [InlineData("legacy-open")]
    [InlineData("legacy-save")]
    public void Navigates_UNC_if_available(string mode)
    {
        if (!UncReachable.Value)
        {
            output.WriteLine($"Skipped: {Unc} is not reachable from this session.");
            return;
        }

        using var host = Open(mode, _a.Path);
        var before = BoxText(host.Dialog, mode);

        var (moved, elapsed) = Navigate(host.Dialog, Unc);
        output.WriteLine($"{mode} -> {Unc}: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.True(moved, host.Transcript);
        AssertReported(host, Unc);
        AssertShows(host, Unc);
        AssertBoxAfterMove(host.Dialog, mode, before, Unc);
    }

    /// <summary>
    /// A legacy folder picker does move to a UNC path, but its tree is read back through the Network
    /// folder, which can take tens of seconds: the call still keeps to its budget, and the dialog is
    /// unharmed. The result is logged, not asserted.
    /// </summary>
    [Theory]
    [InlineData("legacy-folder")]
    [InlineData("legacy-folder-classic")]
    public void Unc_in_a_legacy_folder_picker_keeps_to_the_budget(string mode)
    {
        if (!UncReachable.Value)
        {
            output.WriteLine($"Skipped: {Unc} is not reachable from this session.");
            return;
        }

        using var host = Open(mode, _a.Path);

        var (moved, elapsed) = Navigate(host.Dialog, Unc);
        output.WriteLine($"{mode} -> {Unc}: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms (suspended after: {_sut.IsSuspended})");

        Assert.True(elapsed < FileDialogNavigator.DefaultBudget + Margin, $"{elapsed.TotalMilliseconds:F0} ms");
        Thread.Sleep(300);
        Assert.False(host.HasExited, "DialogHost ended: " + host.Transcript);
    }

    /// <summary>
    /// Locations a dialog cannot browse, which it would validate as its result (an Open dialog closes
    /// on a file path or the Recycle Bin) or answer with an error box: refused before anything is sent.
    /// </summary>
    [Theory]
    [InlineData("open", "recycle-bin")]
    [InlineData("legacy-open", "recycle-bin")]
    [InlineData("save", "recycle-bin")]
    [InlineData("open", "file")]
    [InlineData("legacy-open", "file")]
    [InlineData("save", "file")]
    [InlineData("folder", "file")]
    [InlineData("legacy-folder", "file")]
    [InlineData("open", "zip")]
    [InlineData("open", "missing")]
    [InlineData("legacy-save", "missing")]
    public void Refuses_a_location_the_dialog_cannot_show(string mode, string kind)
    {
        var location = kind switch
        {
            "recycle-bin" => RecycleBin,
            "file" => CreateFile("note.txt"),
            "zip" => CreateFile("archive.zip"),
            _ => Path.Combine(_b.Path, "missing"),
        };
        using var host = Open(mode, _a.Path);
        var before = BoxText(host.Dialog, mode);

        var (moved, elapsed) = Navigate(host.Dialog, location);
        output.WriteLine($"{mode} -> {location}: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.False(moved);
        Thread.Sleep(300);
        Assert.False(host.HasExited, "The dialog closed: " + host.Transcript);
        Assert.Equal(before, BoxText(host.Dialog, mode));
        AssertShows(host, _a.Path);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("legacy-folder")]
    public void A_dialog_already_there_is_left_alone(string mode)
    {
        using var host = Open(mode, _a.Path);
        var before = BoxText(host.Dialog, mode);

        Assert.True(Navigate(host.Dialog, _a.Path + @"\").Moved, host.Transcript);

        Assert.Equal(before, BoxText(host.Dialog, mode));
        Assert.Null(host.WaitForLine(
            l => l.StartsWith("FOLDER ", StringComparison.Ordinal) && !OpenedExplorer.SameFolder(l["FOLDER ".Length..], _a.Path),
            TimeSpan.FromMilliseconds(300)));
    }

    // ---- Verification and retry ----

    /// <summary>
    /// The first validation is dropped, and the dialog puts a text of its own in the box meanwhile
    /// (DialogHost <c>drop reset</c>): the dialog is done with that OK, so a second one follows after
    /// the retry delay, and the dialog moves. A folder picker's selection message is simply sent again.
    /// </summary>
    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("folder")]
    [InlineData("legacy-save")]
    [InlineData("legacy-folder")]
    [InlineData("legacy-folder-classic")]
    public void Retries_once_when_the_first_validation_did_not_take(string mode)
    {
        using var host = Open(mode, _a.Path);
        var before = BoxText(host.Dialog, mode);
        host.Send(IsFolderPicker(mode) ? "drop" : "drop reset");
        Assert.NotNull(host.WaitForLine(l => l == "DROP ARMED", TimeSpan.FromSeconds(5)));

        var (moved, elapsed) = Navigate(host.Dialog, _b.Path);
        output.WriteLine($"{mode}, first validation dropped: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.True(moved, host.Transcript);
        AssertReported(host, _b.Path);
        Assert.Equal(1, Lines(host, "DROPPED"));
        Assert.Equal(1, Lines(host, "PASSED"));
        Assert.True(elapsed >= FileDialogNavigator.RetryDelay, $"{elapsed.TotalMilliseconds:F0} ms");
        Assert.True(elapsed < FileDialogNavigator.DefaultBudget + Margin, $"{elapsed.TotalMilliseconds:F0} ms");
        AssertBoxAfterMove(host.Dialog, mode, before, _b.Path);
    }

    /// <summary>
    /// The first validation is dropped and the box keeps the folder typed (DialogHost <c>drop</c>), as
    /// while a slow move is still under way: no second OK, since the dialog could put its file name
    /// back just before it and have it validated. The call fails within its budget, and the box gets
    /// its text back.
    /// </summary>
    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("legacy-save")]
    public void Never_validates_twice_while_the_first_validation_may_still_be_running(string mode)
    {
        using var host = Open(mode, _a.Path);
        host.Send("drop");
        Assert.NotNull(host.WaitForLine(l => l == "DROP ARMED", TimeSpan.FromSeconds(5)));

        var (moved, elapsed) = Navigate(host.Dialog, _b.Path);
        output.WriteLine($"{mode}, validation dropped, box untouched: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.False(moved);
        Assert.True(elapsed < FileDialogNavigator.DefaultBudget + Margin, $"{elapsed.TotalMilliseconds:F0} ms");
        Thread.Sleep(300);
        Assert.Equal(1, Lines(host, "DROPPED"));
        Assert.Equal(0, Lines(host, "PASSED"));
        Assert.False(host.HasExited, "The dialog closed: " + host.Transcript);
        AssertShows(host, _a.Path);
        Assert.Equal(FileName, BoxText(host.Dialog, mode));
    }

    /// <summary>
    /// The OK is held past its timeout, then handled through a message the dialog posts to itself
    /// (DialogHost <c>slow-ok</c>), so after anything sent to the dialog meanwhile. A text put back
    /// right after the timeout would be in the box when that OK reads it, and would be validated as
    /// the file to open or save: the dialog would close. The OK is waited for instead: the dialog stays
    /// open, on A or B, and its box ends with the file name.
    /// </summary>
    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("legacy-save")]
    public void An_OK_that_times_out_is_waited_for_before_the_text_is_put_back(string mode)
    {
        using var host = Open(mode, _a.Path);
        host.Send("slow-ok 700");
        Assert.NotNull(host.WaitForLine(l => l == "SLOW OK ARMED", TimeSpan.FromSeconds(5)));

        var (moved, elapsed) = Navigate(host.Dialog, _b.Path);
        output.WriteLine($"{mode}, OK held 700 ms then deferred: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.True(elapsed < FileDialogNavigator.DefaultBudget + Margin, $"{elapsed.TotalMilliseconds:F0} ms");
        Thread.Sleep(1000);
        Assert.Equal(1, Lines(host, "SLOW OK"));
        Assert.Equal(0, Lines(host, "CLOSED"));
        Assert.False(host.HasExited, "The dialog closed: " + host.Transcript);
        var current = CurrentFolder(host.Dialog);
        Assert.True(
            OpenedExplorer.SameFolder(current, _a.Path) || OpenedExplorer.SameFolder(current, _b.Path),
            $"The dialog shows '{current}': {host.Transcript}");
        if (moved)
        {
            AssertShows(host, _b.Path);
        }

        Assert.Equal(FileName, BoxText(host.Dialog, mode));
    }

    /// <summary>
    /// The OK is held past the whole budget (DialogHost <c>slow-ok 2000</c>), then handled through a message
    /// the dialog posts to itself, after everything sent to it meanwhile. The navigator gives up while the
    /// dialog still shows nothing of that OK: putting the file name back then would have it validated by
    /// that late OK (Open would open it, Save As save under it, both closing the dialog). The folder stays
    /// in the box instead, so the late OK only moves the dialog, which then puts its own file name back.
    /// </summary>
    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("legacy-save")]
    public void An_OK_still_unanswered_at_the_end_leaves_the_folder_for_it_to_browse_to(string mode)
    {
        using var host = Open(mode, _a.Path);
        host.Send("slow-ok 2000");
        Assert.NotNull(host.WaitForLine(l => l == "SLOW OK ARMED", TimeSpan.FromSeconds(5)));

        var (moved, elapsed) = Navigate(host.Dialog, _b.Path);
        output.WriteLine($"{mode}, OK held 2000 ms then deferred: Navigate {moved} in {elapsed.TotalMilliseconds:F0} ms");

        Assert.False(moved);
        Assert.True(elapsed < FileDialogNavigator.DefaultBudget + Margin, $"{elapsed.TotalMilliseconds:F0} ms");

        // The held OK is handled once the dialog wakes up: it browses to the folder left in the box.
        Assert.NotNull(host.WaitForLine(l => l == "PASSED", TimeSpan.FromSeconds(5)));
        AssertReported(host, _b.Path);
        Thread.Sleep(500);
        Assert.Equal(1, Lines(host, "SLOW OK"));
        Assert.Equal(0, Lines(host, "CLOSED"));
        Assert.False(host.HasExited, "The dialog closed: " + host.Transcript);
        AssertShows(host, _b.Path);
        Assert.Equal(FileName, BoxText(host.Dialog, mode));
    }

    // ---- Budget and threads ----

    [Fact]
    public void An_overrun_returns_false_and_suspends_navigation_until_the_work_ends()
    {
        using var host = Open("open", _a.Path);
        using var release = new ManualResetEventSlim();
        using var sut = new FileDialogNavigator(TimeSpan.FromMilliseconds(200));

        // Work that blocks (as a shell walk through an unreachable network location would): abandoned.
        var watch = Stopwatch.StartNew();
        Assert.False(sut.RunBounded(_ => release.Wait(TimeSpan.FromSeconds(10))));
        output.WriteLine($"overrun returned after {watch.Elapsed.TotalMilliseconds:F0} ms");
        Assert.True(sut.IsSuspended);

        // While it runs, navigations fail at once and touch nothing.
        watch.Restart();
        Assert.False(Sta.Run(() => sut.Navigate(host.Dialog, _b.Path)));
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(100), $"{watch.Elapsed.TotalMilliseconds:F0} ms");
        AssertShows(host, _a.Path);

        // Once it has returned, a new worker navigates.
        release.Set();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (sut.IsSuspended && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Assert.False(sut.IsSuspended);
        Assert.True(Navigate(host.Dialog, _b.Path).Moved, host.Transcript);
        AssertReported(host, _b.Path);
    }

    [Fact]
    public void A_disposed_navigator_returns_false()
    {
        using var host = Open("legacy-open", _a.Path);
        var sut = new FileDialogNavigator();
        sut.Dispose();

        Assert.False(sut.Navigate(host.Dialog, _b.Path));
        AssertShows(host, _a.Path);
    }

    // ---- Helpers ----

    private static bool IsFolderPicker(string mode) => mode.StartsWith("legacy-folder", StringComparison.Ordinal);

    /// <summary>Opens DialogHost on <paramref name="folder"/> and waits until the dialog shows it.</summary>
    private DialogHostProcess Open(string mode, string folder)
    {
        var host = DialogHostProcess.Start(mode, folder, mode == "folder" || IsFolderPicker(mode) ? null : FileName);
        try
        {
            var deadline = DateTime.UtcNow + SettleTimeout;
            string? current;
            while (!OpenedExplorer.SameFolder(current = CurrentFolder(host.Dialog), folder) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
            }

            Assert.True(OpenedExplorer.SameFolder(current, folder), $"The dialog does not show '{folder}' ('{current}'): {host.Transcript}");
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    /// <summary>Navigates from an STA thread, as the UI thread does.</summary>
    private (bool Moved, TimeSpan Elapsed) Navigate(nint dialog, string folder) => Sta.Run(() =>
    {
        var watch = Stopwatch.StartNew();
        var moved = _sut.Navigate(dialog, folder);
        return (moved, watch.Elapsed);
    });

    private static string? CurrentFolder(nint dialog) => Sta.Run(() => FileDialogInspector.CurrentFolder(dialog));

    /// <summary>How many times DialogHost has written that line so far.</summary>
    private static int Lines(DialogHostProcess host, string line) =>
        host.Transcript.Split(" | ").Count(l => l == line);

    private string CreateFile(string name)
    {
        var path = Path.Combine(_b.Path, name);
        File.WriteAllText(path, "FolderSwitch test");
        return path;
    }

    private static void AssertReported(DialogHostProcess host, string folder) =>
        Assert.True(
            host.WaitForLine(l => l.StartsWith("FOLDER ", StringComparison.Ordinal) && OpenedExplorer.SameFolder(l["FOLDER ".Length..], folder), ReportTimeout) is not null,
            $"The dialog did not report '{folder}': {host.Transcript}");

    private static void AssertShows(DialogHostProcess host, string folder)
    {
        var current = CurrentFolder(host.Dialog);
        Assert.True(OpenedExplorer.SameFolder(current, folder), $"Expected the dialog on '{folder}', it shows '{current}': {host.Transcript}");
    }

    /// <summary>
    /// After a move, an Open / Save As box has its original text; the folder picker's box has the
    /// dialog's own (the new folder's name); no box keeps the folder that was typed.
    /// </summary>
    private void AssertBoxAfterMove(nint dialog, string mode, string? before, string folder)
    {
        if (IsFolderPicker(mode))
        {
            return;
        }

        var after = BoxText(dialog, mode);
        output.WriteLine($"file name box after the move: '{after}'");
        Assert.NotNull(after);
        Assert.DoesNotContain(folder.TrimEnd('\\'), after, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@":\", after, StringComparison.Ordinal);
        if (mode != "folder")
        {
            Assert.Equal(before, after);
        }
    }

    /// <summary>The text of the file name box (none for a legacy folder picker), found at its measured place.</summary>
    private static string? BoxText(nint dialog, string mode) =>
        IsFolderPicker(mode) ? null : Win32.Text(FileNameBox(dialog, mode));

    private static void SetBoxText(nint dialog, string mode, string text)
    {
        var box = FileNameBox(dialog, mode);
        Assert.True(Win32.SendMessageTimeout(box, Win32.WmSetText, 0, text, Win32.SmtoAbortIfHung, 1000, out _) != 0);
        Assert.Equal(text, Win32.Text(box));
    }

    /// <summary>
    /// The edit of the file name box, as measured with DialogHost on Windows 11: the folder picker's
    /// <c>Edit</c> 0x480; Save As (Common Item Dialog): DirectUI's first <c>FloatNotifySink</c>
    /// &gt; <c>ComboBox</c> &gt; <c>Edit</c>; the others: <c>ComboBoxEx32</c> 0x47C &gt; <c>ComboBox</c> &gt; <c>Edit</c>.
    /// </summary>
    private static nint FileNameBox(nint dialog, string mode)
    {
        var box = mode switch
        {
            "folder" => Win32.GetDlgItem(dialog, 0x480),
            "save" => Win32.Descend(dialog, "DUIViewWndClassName", "DirectUIHWND", "FloatNotifySink", "ComboBox", "Edit"),
            _ => Win32.Descend(Win32.GetDlgItem(dialog, 0x47C), "ComboBox", "Edit"),
        };
        Assert.True(box != 0, $"No file name box in the {mode} dialog.");
        return box;
    }

    private static class Win32
    {
        public const uint WmSetText = 0x000C;
        public const uint SmtoAbortIfHung = 0x0002;
        public const int MessageBoxTextId = 0xFFFF;

        private const uint WmGetText = 0x000D;

        /// <summary>The window text through <c>WM_GETTEXT</c> (what a control shows), or <c>null</c>.</summary>
        public static string? Text(nint window)
        {
            var buffer = new StringBuilder(4096);
            return window != 0 && SendMessageTimeout(window, WmGetText, buffer.Capacity, buffer, SmtoAbortIfHung, 1000, out _) != 0
                ? buffer.ToString()
                : null;
        }

        /// <summary>Follows first children of the given classes from <paramref name="root"/>; zero if one is missing.</summary>
        public static nint Descend(nint root, params string[] classes)
        {
            var window = root;
            foreach (var className in classes)
            {
                window = window == 0 ? 0 : FindWindowEx(window, 0, className, null);
            }

            return window;
        }

        [DllImport("user32.dll")]
        public static extern nint GetDlgItem(nint dialog, int id);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(nint hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageTimeoutW")]
        public static extern nint SendMessageTimeout(nint hwnd, uint msg, nint wParam, string lParam, uint flags, uint timeout, out nint result);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageTimeoutW")]
        private static extern nint SendMessageTimeout(nint hwnd, uint msg, nint wParam, StringBuilder lParam, uint flags, uint timeout, out nint result);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")]
        private static extern nint FindWindowEx(nint parent, nint after, string? className, string? windowName);
    }
}
