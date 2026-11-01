using System.Diagnostics;
using FolderSwitch.Core.Activation;
using FolderSwitch.Windows.Dialogs;
using FolderSwitch.Windows.IntegrationTests.Support;
using FolderSwitch.Windows.Shell;
using FolderSwitch.Windows.SystemServices;
using Xunit.Abstractions;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Runs on the real desktop session: opens Explorer windows (closed afterwards, the tab only when
/// Windows 11 opened the folder as a tab of an existing window), DialogHost dialogs and a message box,
/// brings each one to the foreground (see <see cref="Foreground"/>: no keystroke is sent) and captures
/// the context. Every window opened is closed, also when an assertion fails.
/// </summary>
/// <remarks>
/// A capture is retried for a short while until it shows the expected folder: a freshly opened window
/// may still be initializing, and the very first lookups of the process run cold (COM and shell
/// start-up) and can exceed the 50 ms budget. <see cref="A_warm_capture_reads_the_folder_within_the_budget"/>
/// checks the steady state without retries.
/// </remarks>
[Trait("Category", "Desktop")]
[Collection(ForegroundCollection.Name)]
public sealed class CallContextProviderTests(ITestOutputHelper output) : IDisposable
{
    private const string ThisPc = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(3);

    private readonly CallContextProvider _sut = new();
    private readonly TempFolder _folder = new();

    public void Dispose()
    {
        _sut.Dispose();
        _folder.Dispose();
    }

    // ---- Brief tests ----

    [Fact]
    public void Explorer_window_is_detected_with_its_folder()
    {
        using var explorer = OpenedExplorer.Open(_folder.Path, _folder.Path);
        output.WriteLine($"Explorer window 0x{explorer.Window:X}, tab 0x{explorer.Tab:X}, new window: {explorer.IsNewWindow}");

        var context = CaptureUntilFolder(explorer.Window, _folder.Path);

        Assert.Equal(CallContextKind.Explorer, context.Kind);
        Assert.Equal(explorer.Window, context.Window);
        AssertFolder(_folder.Path, context);
    }

    [Fact]
    public void Explorer_reports_the_folder_of_its_active_tab()
    {
        if (Environment.OSVersion.Version.Build < 22000)
        {
            return; // No tabs before Windows 11.
        }

        using var second = new TempFolder();
        using var explorer = OpenedExplorer.Open(_folder.Path, _folder.Path);
        using var tab = OpenedExplorer.OpenTab(explorer, second.Path);
        output.WriteLine($"Explorer window 0x{explorer.Window:X}: tabs 0x{explorer.Tab:X} and 0x{tab.Tab:X}");

        // The new tab is the active one: its folder, not the first tab's.
        var context = CaptureUntilFolder(explorer.Window, second.Path);
        Assert.Equal(CallContextKind.Explorer, context.Kind);
        Assert.Equal(explorer.Window, context.Window);
        AssertFolder(second.Path, context);

        // Closing that tab leaves the first one, which becomes the active tab again.
        tab.Dispose();
        AssertFolder(_folder.Path, CaptureUntilFolder(explorer.Window, _folder.Path));
    }

    [Fact]
    public void Explorer_on_ThisPC_reports_shell_form()
    {
        using var explorer = OpenedExplorer.Open(ThisPc, ThisPc);

        var context = CaptureUntilFolder(explorer.Window, ThisPc);

        Assert.Equal(CallContextKind.Explorer, context.Kind);
        Assert.Equal(explorer.Window, context.Window);
        Assert.Equal(ThisPc, context.CurrentFolder, ignoreCase: true);

        // And the name Explorer shows for it (« This PC », « Ce PC »... in the language of Windows).
        output.WriteLine($"This PC is shown as '{context.CurrentFolderName}'");
        Assert.False(string.IsNullOrWhiteSpace(context.CurrentFolderName));
        Assert.DoesNotContain("::", context.CurrentFolderName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("legacy-open")]
    public void A_dialog_on_a_virtual_location_reports_its_shell_name(string mode)
    {
        using var host = DialogHostProcess.Start(mode, ThisPc);

        var context = CaptureUntilFolder(host.Dialog, ThisPc);

        output.WriteLine($"{mode}: This PC is shown as '{context.CurrentFolderName}'");
        Assert.False(string.IsNullOrWhiteSpace(context.CurrentFolderName));
        Assert.DoesNotContain("::", context.CurrentFolderName, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dialog_on_a_path_reports_no_shell_name()
    {
        // A path names itself: no second shell lookup is spent on it within the budget.
        using var host = DialogHostProcess.Start("open", _folder.Path);

        var context = CaptureUntilFolder(host.Dialog, _folder.Path);

        Assert.Null(context.CurrentFolderName);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("folder")]
    [InlineData("legacy-open")]
    [InlineData("legacy-save")]
    [InlineData("legacy-folder")]
    [InlineData("legacy-folder-classic")]
    public void Each_dialog_kind_is_detected(string mode)
    {
        using var host = DialogHostProcess.Start(mode, _folder.Path);

        var context = CaptureUntilFolder(host.Dialog, _folder.Path);

        Assert.Equal(CallContextKind.FileDialog, context.Kind);
        Assert.Equal(host.Dialog, context.Window);
        AssertFolder(_folder.Path, context, host.Transcript);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("save")]
    [InlineData("folder")]
    [InlineData("legacy-open")]
    [InlineData("legacy-save")]
    [InlineData("legacy-folder")]
    [InlineData("legacy-folder-classic")]
    public void Dialog_on_ThisPC_reports_shell_form(string mode)
    {
        using var host = DialogHostProcess.Start(mode, ThisPc);

        var context = CaptureUntilFolder(host.Dialog, ThisPc);

        Assert.Equal(CallContextKind.FileDialog, context.Kind);
        AssertFolder(ThisPc, context, host.Transcript);
    }

    [Fact]
    public void MessageBox_is_Other()
    {
        using var box = MessageBoxScope.Show();
        Foreground.Bring(box.Window);
        var snapshot = WindowInspector.Snapshot(box.Window);
        output.WriteLine($"{snapshot.ClassName} ({snapshot.ProcessName}): {string.Join(", ", snapshot.DescendantClassNames)}");

        var context = _sut.CaptureForeground();

        Assert.Equal(CallContextKind.Other, context.Kind);
        Assert.Equal(box.Window, context.Window);
        Assert.Null(context.CurrentFolder);

        // Asked directly, the inspector classifies it first and reads nothing.
        Assert.Null(Sta.Run(() => FileDialogInspector.CurrentFolder(box.Window)));
    }

    // ---- Browsers ----

    [BrowserFact(BrowserKind.Firefox)]
    public void Browser_window_gives_browser_context_without_folder_Firefox() => AssertBrowserContext(BrowserKind.Firefox);

    [BrowserFact(BrowserKind.Chrome)]
    public void Browser_window_gives_browser_context_without_folder_Chrome() => AssertBrowserContext(BrowserKind.Chrome);

    [BrowserFact(BrowserKind.Edge)]
    public void Browser_window_gives_browser_context_without_folder_Edge() => AssertBrowserContext(BrowserKind.Edge);

    // ---- Budget and threads ----

    [Fact]
    public void A_warm_capture_reads_the_folder_within_the_budget()
    {
        using var host = DialogHostProcess.Start("open", _folder.Path);
        CaptureUntilFolder(host.Dialog, _folder.Path);

        // Steady state: a dialog that has just opened is still busy (its view filling), and a message
        // to it may wait past the budget.
        Thread.Sleep(1000);

        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            var context = _sut.CaptureForeground();
            watch.Stop();
            output.WriteLine($"warm capture {i}: {watch.Elapsed.TotalMilliseconds:F1} ms, {context}");

            AssertFolder(_folder.Path, context);

            // The margin only covers thread scheduling.
            Assert.True(watch.Elapsed < CallContextProvider.DefaultBudget + TimeSpan.FromMilliseconds(15), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
        }
    }

    [Fact]
    public void A_warm_capture_reads_the_Explorer_folder_within_the_budget()
    {
        using var explorer = OpenedExplorer.Open(_folder.Path, _folder.Path);
        CaptureUntilFolder(explorer.Window, _folder.Path);

        // Steady state, as for the dialog: a window that has just opened is still busy.
        Thread.Sleep(1000);

        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            var context = _sut.CaptureForeground();
            watch.Stop();
            output.WriteLine($"warm capture {i}: {watch.Elapsed.TotalMilliseconds:F1} ms, {context}");

            Assert.Equal(explorer.Window, context.Window);
            AssertFolder(_folder.Path, context);
            Assert.True(watch.Elapsed < CallContextProvider.DefaultBudget + TimeSpan.FromMilliseconds(15), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
        }
    }

    [Fact]
    public void A_dialog_that_does_not_answer_costs_no_more_than_the_budget()
    {
        using var host = DialogHostProcess.Start("open", _folder.Path);
        CaptureUntilFolder(host.Dialog, _folder.Path);
        host.Send("hang 1500");
        Assert.NotNull(host.WaitForLine(l => l == "HUNG", TimeSpan.FromSeconds(5)));

        var watch = Stopwatch.StartNew();
        var context = _sut.CaptureForeground();
        watch.Stop();
        output.WriteLine($"capture of a hung dialog: {watch.Elapsed.TotalMilliseconds:F1} ms, {context}");

        Assert.Equal(new CallContext(CallContextKind.FileDialog, host.Dialog, null), context);
        Assert.True(watch.Elapsed < CallContextProvider.DefaultBudget + TimeSpan.FromMilliseconds(15), $"{watch.Elapsed.TotalMilliseconds:F1} ms");

        // Once the dialog answers again, a new lookup thread reads its folder.
        Assert.NotNull(host.WaitForLine(l => l == "AWAKE", TimeSpan.FromSeconds(5)));
        AssertFolder(_folder.Path, CaptureUntilFolder(host.Dialog, _folder.Path));
    }

    /// <summary>
    /// The legacy dialogs, whose lookups send messages that carry a pointer into the dialog's process:
    /// while the dialog is hung, the capture keeps to the budget, and the dialog goes on working once
    /// awake (a message still queued when its sender gave up is not delivered afterwards).
    /// </summary>
    [Theory]
    [InlineData("legacy-open")]
    [InlineData("legacy-folder")]
    [InlineData("legacy-folder-classic")]
    public void A_hung_legacy_dialog_costs_the_budget_and_survives(string mode)
    {
        using var host = DialogHostProcess.Start(mode, _folder.Path);
        CaptureUntilFolder(host.Dialog, _folder.Path);
        var readThroughPointer = PointerRead(mode, host.Dialog);
        host.Send("hang 1500");
        Assert.NotNull(host.WaitForLine(l => l == "HUNG", TimeSpan.FromSeconds(5)));

        var watch = Stopwatch.StartNew();
        var context = _sut.CaptureForeground();
        watch.Stop();
        output.WriteLine($"capture of the hung dialog: {watch.Elapsed.TotalMilliseconds:F1} ms, {context}");
        Assert.Equal(new CallContext(CallContextKind.FileDialog, host.Dialog, null), context);
        Assert.True(watch.Elapsed < CallContextProvider.DefaultBudget + TimeSpan.FromMilliseconds(15), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
        Assert.Null(Sta.Run(readThroughPointer));

        Assert.NotNull(host.WaitForLine(l => l == "AWAKE", TimeSpan.FromSeconds(5)));
        Thread.Sleep(500);
        Assert.False(host.HasExited, "DialogHost ended: " + host.Transcript);
        AssertFolder(_folder.Path, CaptureUntilFolder(host.Dialog, _folder.Path));
        Assert.Equal(0, host.QuitAndWait());
    }

    /// <summary>
    /// The race behind a timed-out message with a pointer: the dialog has already taken the message
    /// when the sender gives up (DialogHost <c>slow</c> holds it after taking it), then writes through
    /// the pointer. The block must still be allocated then; freeing it after the timeout made the
    /// dialog fault in its own process.
    /// </summary>
    [Theory]
    [InlineData("legacy-open")]
    [InlineData("legacy-folder")]
    [InlineData("legacy-folder-classic")]
    public void A_dialog_answering_after_the_timeout_writes_to_memory_still_allocated(string mode)
    {
        using var host = DialogHostProcess.Start(mode, _folder.Path);
        CaptureUntilFolder(host.Dialog, _folder.Path);
        var readThroughPointer = PointerRead(mode, host.Dialog);
        host.Send("slow 600");
        Assert.NotNull(host.WaitForLine(l => l == "SLOW ARMED", TimeSpan.FromSeconds(5)));

        var watch = Stopwatch.StartNew();
        Assert.Null(Sta.Run(readThroughPointer));
        output.WriteLine($"read through a pointer: gave up after {watch.Elapsed.TotalMilliseconds:F1} ms");

        Assert.True(
            host.WaitForLine(l => l == "SLOW DONE", TimeSpan.FromSeconds(5)) is not null,
            $"The dialog did not finish handling the message (DialogHost exited: {host.HasExited}): {host.Transcript}");
        Thread.Sleep(300);
        Assert.False(host.HasExited, "DialogHost ended: " + host.Transcript);
        AssertFolder(_folder.Path, CaptureUntilFolder(host.Dialog, _folder.Path));
        Assert.Equal(0, host.QuitAndWait());
    }

    [Fact]
    public void An_abandoned_lookup_still_running_makes_captures_skip_the_lookup()
    {
        using var host = DialogHostProcess.Start("open", _folder.Path);
        using var release = new ManualResetEventSlim();
        var calls = 0;
        using var sut = new CallContextProvider(
            CallContextProvider.DefaultBudget,
            (_, _) =>
            {
                Interlocked.Increment(ref calls);
                release.Wait(TimeSpan.FromSeconds(10));
                return "released";
            });

        // The lookup blocks (as a COM call into a hung Explorer window would): overrun, abandoned.
        Assert.Equal(new CallContext(CallContextKind.FileDialog, host.Dialog, null), sut.Capture(host.Dialog));
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.True(sut.IsLookupSuspended);

        // While it still runs, captures do not start another one: at once, kind kept, no folder.
        for (var i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            var context = sut.Capture(host.Dialog);
            watch.Stop();
            output.WriteLine($"capture while suspended: {watch.Elapsed.TotalMilliseconds:F1} ms");

            Assert.Equal(new CallContext(CallContextKind.FileDialog, host.Dialog, null), context);
            Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(20), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
        }

        Assert.Equal(1, Volatile.Read(ref calls));

        // Once it has returned, the next capture runs a lookup on a new thread.
        release.Set();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (sut.IsLookupSuspended && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }

        Assert.False(sut.IsLookupSuspended);
        Assert.Equal(new CallContext(CallContextKind.FileDialog, host.Dialog, "released"), sut.Capture(host.Dialog));
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    [Fact]
    public void Lookup_over_budget_keeps_the_kind_without_folder()
    {
        using var host = DialogHostProcess.Start("open", _folder.Path);
        Foreground.Bring(host.Dialog);
        using var sut = new CallContextProvider(TimeSpan.Zero);

        var context = sut.CaptureForeground();

        Assert.Equal(new CallContext(CallContextKind.FileDialog, host.Dialog, null), context);
    }

    [Fact]
    public void Lookup_over_budget_keeps_an_Explorer_context()
    {
        using var explorer = OpenedExplorer.Open(_folder.Path, _folder.Path);
        using var sut = new CallContextProvider(TimeSpan.Zero);

        var context = sut.Capture(explorer.Window);

        Assert.Equal(new CallContext(CallContextKind.Explorer, explorer.Window, null), context);
    }

    [Fact]
    public void Capture_from_an_STA_thread_gives_the_same_context()
    {
        using var host = DialogHostProcess.Start("legacy-open", _folder.Path);
        CaptureUntilFolder(host.Dialog, _folder.Path);

        var context = Sta.Run(_sut.CaptureForeground);

        Assert.Equal(CallContextKind.FileDialog, context.Kind);
        Assert.Equal(host.Dialog, context.Window);
        AssertFolder(_folder.Path, context);
    }

    [Fact]
    public void A_closed_window_or_no_window_gives_None()
    {
        nint closed;
        using (var box = MessageBoxScope.Show())
        {
            closed = box.Window;
        }

        Assert.Same(CallContext.None, _sut.Capture(closed));
        Assert.Same(CallContext.None, _sut.Capture(0));
    }

    [Fact]
    public void A_disposed_provider_gives_None()
    {
        var sut = new CallContextProvider();
        sut.Dispose();

        Assert.Same(CallContext.None, sut.CaptureForeground());
    }

    // ---- Helpers ----

    /// <summary>
    /// Starts the browser alone (new profile, see <see cref="BrowserSession"/>), brings its window to the foreground and
    /// captures: a browser context with the browser, the window, and no folder, within the budget once warm.
    /// </summary>
    private void AssertBrowserContext(BrowserKind browser)
    {
        using var session = BrowserSession.Start(browser);
        var snapshot = WindowInspector.Snapshot(session.Window);
        output.WriteLine($"{browser}: window 0x{session.Window:X}, {snapshot.ClassName} ({snapshot.ProcessName}), {snapshot.DescendantClassNames.Count} descendants");

        // A freshly started window may not be able to take the foreground at once, and the first captures of the
        // process run cold: the first right one is the check of the context, the next ones are timed.
        var deadline = DateTime.UtcNow + SettleTimeout;
        CallContext context;
        do
        {
            Foreground.Bring(session.Window);
            context = _sut.CaptureForeground();
            output.WriteLine($"capture: {context}");
            if (context.Kind == CallContextKind.Browser || DateTime.UtcNow > deadline)
            {
                break;
            }

            Thread.Sleep(100);
        }
        while (true);

        Assert.Equal(CallContextKind.Browser, context.Kind);
        Assert.Equal(browser, context.Browser);
        Assert.Equal(session.Window, context.Window);
        Assert.Null(context.CurrentFolder);
        Assert.Null(context.CurrentFolderName);

        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            var warm = _sut.CaptureForeground();
            watch.Stop();
            output.WriteLine($"warm capture {i}: {watch.Elapsed.TotalMilliseconds:F1} ms, {warm}");

            Assert.Equal(context, warm);
            Assert.True(watch.Elapsed < CallContextProvider.DefaultBudget + TimeSpan.FromMilliseconds(15), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
        }
    }

    /// <summary>
    /// Brings <paramref name="window"/> to the foreground and captures until the context shows
    /// <paramref name="expectedFolder"/> or <see cref="SettleTimeout"/> expires; the last capture.
    /// </summary>
    private CallContext CaptureUntilFolder(nint window, string expectedFolder)
    {
        Foreground.Bring(window);
        var snapshot = WindowInspector.Snapshot(window);
        output.WriteLine(
            $"{snapshot.ClassName} ({snapshot.ProcessName}), {snapshot.DescendantClassNames.Count} descendants: "
            + string.Join(", ", snapshot.DescendantClassNames.Zip(snapshot.DescendantControlIds, (c, id) => id == 0 ? c : $"{c}#{id:X}").Distinct()));
        var deadline = DateTime.UtcNow + SettleTimeout;
        var attempts = 0;
        while (true)
        {
            if (Foreground.Current != window)
            {
                Foreground.Bring(window);
            }

            var watch = Stopwatch.StartNew();
            var context = _sut.CaptureForeground();
            watch.Stop();
            attempts++;
            output.WriteLine($"capture {attempts}: {watch.Elapsed.TotalMilliseconds:F1} ms, {context}");

            if (OpenedExplorer.SameFolder(context.CurrentFolder, expectedFolder) || DateTime.UtcNow > deadline)
            {
                return context;
            }

            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// The inspector's read that sends a pointer into the dialog's process (<c>CDM_GETFOLDERIDLIST</c>
    /// with a buffer, or <c>TVM_GETITEMW</c>), prepared while the dialog answers.
    /// </summary>
    private static Func<string?> PointerRead(string mode, nint dialog)
    {
        if (mode == "legacy-open")
        {
            var size = (int)(FileDialogInspector.FolderIdListSize(dialog) ?? 0);
            Assert.True(size > 2, "No folder ID list.");
            return () => FileDialogInspector.ReadFolderIdList(dialog, size);
        }

        var tree = FileDialogInspector.FolderPickerTree(dialog);
        var item = FileDialogInspector.SelectedTreeItem(tree) ?? 0;
        Assert.NotEqual(0u, item);
        return () => FileDialogInspector.ReadTreeItemText(tree, item);
    }

    private static void AssertFolder(string expected, CallContext context, string? details = null) =>
        Assert.True(
            OpenedExplorer.SameFolder(context.CurrentFolder, expected),
            $"Expected folder '{expected}', got '{context.CurrentFolder}' ({context}). {details}");
}
