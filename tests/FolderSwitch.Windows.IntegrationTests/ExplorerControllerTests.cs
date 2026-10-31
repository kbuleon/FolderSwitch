using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FolderSwitch.Windows.IntegrationTests.Support;
using FolderSwitch.Windows.Shell;
using Xunit.Abstractions;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// Runs on the real desktop session: opens Explorer windows on temporary folders (or tabs, when
/// Windows 11 opens folders in tabs of an existing window), drives them with the controller, then
/// closes every window or tab the test opened, also when an assertion fails. No keystroke is sent.
/// </summary>
[Trait("Category", "Desktop")]
[Collection(ForegroundCollection.Name)]
public sealed class ExplorerControllerTests(ITestOutputHelper output) : IDisposable
{
    private const string ThisPc = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    private const string Unc = @"\\localhost\C$";

    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(10);

    private static readonly Lazy<bool> UncReachable = new(() =>
    {
        var check = Task.Run(() => Directory.Exists(Unc));
        return check.Wait(TimeSpan.FromSeconds(5)) && check.Result;
    });

    private static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    private readonly ExplorerController _sut = new();
    private readonly TempFolder _a = new();
    private readonly TempFolder _b = new();

    public void Dispose()
    {
        _sut.Dispose();
        _a.Dispose();
        _b.Dispose();
    }

    // ---- Navigate ----

    [Fact]
    public void Navigate_changes_active_tab_location()
    {
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);
        output.WriteLine($"Explorer window 0x{explorer.Window:X}, tab 0x{explorer.Tab:X}, new window: {explorer.IsNewWindow}");

        var watch = Stopwatch.StartNew();
        Assert.True(_sut.Navigate(explorer.Window, _b.Path));
        output.WriteLine($"Navigate: {watch.Elapsed.TotalMilliseconds:F0} ms");

        // The same browser (same tab window) now shows B.
        AssertFolder(_b.Path, explorer.WaitForFolder(_b.Path, SettleTimeout));
    }

    [Fact]
    public void Navigate_works_from_an_STA_thread()
    {
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);

        Assert.True(Sta.Run(() => _sut.Navigate(explorer.Window, _b.Path)));

        AssertFolder(_b.Path, explorer.WaitForFolder(_b.Path, SettleTimeout));
    }

    [Theory]
    [InlineData("shell:MyComputerFolder", ThisPc)]
    [InlineData("shell:RecycleBinFolder", RecycleBin)]
    [InlineData(RecycleBin, RecycleBin)]
    [InlineData(ThisPc, ThisPc)]
    public void Navigate_accepts_shell_forms(string location, string expectedFolder)
    {
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);

        Assert.True(_sut.Navigate(explorer.Window, location));

        AssertFolder(expectedFolder, explorer.WaitForFolder(expectedFolder, SettleTimeout));
    }

    [Fact]
    public void Navigate_accepts_a_unc_path()
    {
        if (!UncReachable.Value)
        {
            output.WriteLine($"Skipped: {Unc} is not reachable from this session.");
            return;
        }

        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);

        var watch = Stopwatch.StartNew();
        Assert.True(_sut.Navigate(explorer.Window, Unc));
        output.WriteLine($"Navigate: {watch.Elapsed.TotalMilliseconds:F0} ms");

        AssertFolder(Unc, explorer.WaitForFolder(Unc, SettleTimeout));
    }

    [Fact]
    public void Navigate_drives_the_active_tab_only()
    {
        if (!IsWindows11)
        {
            output.WriteLine("Skipped: no tabs before Windows 11.");
            return;
        }

        using var c = new TempFolder();
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);
        using var tab = OpenedExplorer.OpenTab(explorer, _b.Path);

        Assert.True(_sut.Navigate(explorer.Window, c.Path));

        // The new tab is the active one: it moves, the first one stays where it was.
        AssertFolder(c.Path, tab.WaitForFolder(c.Path, SettleTimeout));
        AssertFolder(_a.Path, explorer.CurrentFolder());
    }

    [Fact]
    public void Navigate_restores_a_minimized_window_and_brings_it_to_the_foreground()
    {
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);
        if (!explorer.IsNewWindow)
        {
            output.WriteLine("Skipped: the folder opened as a tab of an existing window, which the test does not minimize.");
            return;
        }

        // The test process takes the foreground, as FolderSwitch has it when its item is clicked.
        using var box = MessageBoxScope.Show();
        Foreground.Bring(box.Window);
        Assert.True(Win32.ShowWindow(explorer.Window, Win32.SwMinimize) || Win32.IsIconic(explorer.Window));
        Assert.True(WaitUntil(() => Win32.IsIconic(explorer.Window), TimeSpan.FromSeconds(5)), "The window did not minimize.");

        Assert.True(_sut.Navigate(explorer.Window, _b.Path));

        Assert.True(
            WaitUntil(() => !Win32.IsIconic(explorer.Window) && Foreground.Current == explorer.Window, TimeSpan.FromSeconds(5)),
            $"Minimized: {Win32.IsIconic(explorer.Window)}, foreground: 0x{Foreground.Current:X}, window: 0x{explorer.Window:X}.");
        AssertFolder(_b.Path, explorer.WaitForFolder(_b.Path, SettleTimeout));
    }

    [Fact]
    public void Navigate_and_OpenInNewTab_return_false_for_a_window_that_is_not_Explorer()
    {
        using var box = MessageBoxScope.Show();

        Assert.False(_sut.Navigate(0, _b.Path));
        Assert.False(_sut.Navigate(box.Window, _b.Path));
        Assert.False(_sut.OpenInNewTab(0, _b.Path));
        Assert.False(_sut.OpenInNewTab(box.Window, _b.Path));
    }

    [Fact]
    public void Navigate_returns_false_for_a_location_that_does_not_parse()
    {
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);

        Assert.False(_sut.Navigate(explorer.Window, Path.Combine(_b.Path, "missing")));
        Assert.False(_sut.Navigate(explorer.Window, "shell:FolderSwitchNoSuchFolder"));
        Assert.False(_sut.Navigate(explorer.Window, "::{00000000-0000-0000-0000-00000000F00D}"));

        Thread.Sleep(500);
        AssertFolder(_a.Path, explorer.CurrentFolder());
    }

    /// <summary>
    /// Work that overruns (as a COM call into a hung Explorer window would) is abandoned: the call
    /// returns <c>false</c> within the budget, and later navigations fail at once without driving the
    /// window until that work has ended.
    /// </summary>
    [Fact]
    public void An_overrun_returns_false_and_suspends_navigation_until_the_work_ends()
    {
        var budget = TimeSpan.FromMilliseconds(200);
        using var sut = new ExplorerController(budget, tabsSupported: IsWindows11);
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);
        using var release = new ManualResetEventSlim();

        var watch = Stopwatch.StartNew();
        Assert.False(sut.RunBounded(_ => release.Wait(TimeSpan.FromSeconds(10))));
        watch.Stop();
        output.WriteLine($"overrun: gave up after {watch.Elapsed.TotalMilliseconds:F1} ms");
        Assert.True(watch.Elapsed < budget + TimeSpan.FromMilliseconds(100), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
        Assert.True(sut.IsSuspended);

        // While it runs: no navigation, at once.
        watch.Restart();
        Assert.False(sut.Navigate(explorer.Window, _b.Path));
        Assert.False(sut.OpenInNewTab(explorer.Window, _b.Path));
        watch.Stop();
        Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(50), $"{watch.Elapsed.TotalMilliseconds:F1} ms");
        Thread.Sleep(500);
        AssertFolder(_a.Path, explorer.CurrentFolder());

        // Once it has returned, a new worker navigates.
        release.Set();
        Assert.True(WaitUntil(() => !sut.IsSuspended, TimeSpan.FromSeconds(5)));
        var navigated = false;
        for (var attempt = 0; attempt < 5 && !navigated; attempt++)
        {
            // The first COM calls of a new thread are slow: a 200 ms budget may not cover them.
            navigated = sut.Navigate(explorer.Window, _b.Path);
            if (!navigated)
            {
                Assert.True(WaitUntil(() => !sut.IsSuspended, TimeSpan.FromSeconds(5)));
            }
        }

        Assert.True(navigated);
        AssertFolder(_b.Path, explorer.WaitForFolder(_b.Path, SettleTimeout));
    }

    // ---- OpenInNewTab ----

    [Fact]
    public void OpenInNewTab_adds_a_tab_or_returns_false()
    {
        if (!IsWindows11)
        {
            output.WriteLine("Skipped: no tabs before Windows 11.");
            return;
        }

        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);
        var before = OpenedExplorer.Snapshot();
        try
        {
            var watch = Stopwatch.StartNew();
            var opened = _sut.OpenInNewTab(explorer.Window, _b.Path);
            output.WriteLine($"OpenInNewTab: {opened} after {watch.Elapsed.TotalMilliseconds:F0} ms");

            if (opened)
            {
                using var tab = OpenedExplorer.WaitForNew(before, _b.Path, SettleTimeout, explorer.Window);
                Assert.NotNull(tab);
                Assert.False(tab.IsNewWindow);

                // The new tab is the active one.
                AssertFolder(_b.Path, Sta.Run(() => ExplorerWindows.CurrentFolder(explorer.Window)));
                AssertFolder(_a.Path, explorer.CurrentFolder());
            }
            else
            {
                // Best effort: false is allowed. A tab that appears late is closed in finally.
                Thread.Sleep(2000);
                output.WriteLine($"New tabs after giving up: [{string.Join(", ", OpenedExplorer.NewFolders(before, explorer.Window))}]");
            }
        }
        finally
        {
            OpenedExplorer.CloseNew(before, explorer.Window);
        }
    }

    /// <summary>
    /// The call overruns right after the new-tab command (the seam holds the worker past the budget):
    /// the caller gets <c>false</c> and falls back to a new window, so the work, going on, closes the
    /// tab that appeared instead of navigating it.
    /// </summary>
    [Fact]
    public void An_abandoned_new_tab_is_closed_instead_of_navigated()
    {
        if (!IsWindows11)
        {
            output.WriteLine("Skipped: no tabs before Windows 11.");
            return;
        }

        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);
        var before = OpenedExplorer.Snapshot();
        var tabSeenDuringOverrun = false;
        using var sut = new ExplorerController(TimeSpan.FromSeconds(1.5), tabsSupported: true)
        {
            AfterNewTabCommand = () =>
            {
                Thread.Sleep(2500);

                // On the worker (an STA thread): the tab the command opened is there by now.
                var browsers = ExplorerWindows.ForWindow(explorer.Window);
                tabSeenDuringOverrun = browsers.Exists(b => !before.Contains((b.TopLevelWindow, b.TabWindow)));
                browsers.ForEach(b => b.Dispose());
            },
        };
        try
        {
            Assert.False(sut.OpenInNewTab(explorer.Window, _b.Path));
            Assert.True(sut.IsSuspended);

            Assert.True(WaitUntil(() => !sut.IsSuspended, TimeSpan.FromSeconds(10)), "The abandoned work did not end.");
            Assert.True(tabSeenDuringOverrun, "The new-tab command opened no tab: the late path was not exercised.");
            Assert.True(
                WaitUntil(() => OpenedExplorer.NewFolders(before, explorer.Window).Count == 0, TimeSpan.FromSeconds(5)),
                $"Late tab left open: [{string.Join(", ", OpenedExplorer.NewFolders(before, explorer.Window))}]");
            AssertFolder(_a.Path, explorer.CurrentFolder());
        }
        finally
        {
            OpenedExplorer.CloseNew(before, explorer.Window);
        }
    }

    [Fact]
    public void OpenInNewTab_does_nothing_without_tabs()
    {
        using var sut = new ExplorerController(ExplorerController.DefaultBudget, tabsSupported: false);
        using var explorer = OpenedExplorer.Open(_a.Path, _a.Path);
        var before = OpenedExplorer.Snapshot();
        try
        {
            Assert.False(sut.OpenInNewTab(explorer.Window, _b.Path));

            Thread.Sleep(1000);
            Assert.Empty(OpenedExplorer.NewFolders(before, explorer.Window));
        }
        finally
        {
            OpenedExplorer.CloseNew(before, explorer.Window);
        }
    }

    // ---- OpenNewWindow ----

    [Fact]
    public void OpenNewWindow_opens_unc_and_shell_forms()
    {
        // From an STA thread (the UI thread's case: ShellExecuteEx on the calling thread)...
        using (OpenedExplorer.OpenBy(() => Sta.Run(() => _sut.OpenNewWindow(_a.Path)), _a.Path))
        {
        }

        // ... and from the test's MTA thread (on a short-lived STA thread).
        using (OpenedExplorer.OpenBy(() => _sut.OpenNewWindow("shell:MyComputerFolder"), ThisPc))
        {
        }

        using (OpenedExplorer.OpenBy(() => _sut.OpenNewWindow(RecycleBin), RecycleBin))
        {
        }

        if (UncReachable.Value)
        {
            using (OpenedExplorer.OpenBy(() => _sut.OpenNewWindow(Unc), Unc))
            {
            }
        }
        else
        {
            output.WriteLine($"UNC part skipped: {Unc} is not reachable from this session.");
        }
    }

    [Fact]
    public void OpenNewWindow_throws_for_a_location_that_does_not_exist()
    {
        var before = OpenedExplorer.Snapshot();
        try
        {
            var missing = Path.Combine(_a.Path, "missing");
            var error = Assert.Throws<Win32Exception>(() => _sut.OpenNewWindow(missing));
            output.WriteLine(error.Message);
            Assert.Throws<Win32Exception>(() => _sut.OpenNewWindow("shell:FolderSwitchNoSuchFolder"));

            Thread.Sleep(1000);
            Assert.Empty(OpenedExplorer.NewFolders(before, 0));
        }
        finally
        {
            OpenedExplorer.CloseNew(before, 0);
        }
    }

    // ---- Helpers ----

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(50);
        }

        return true;
    }

    private static void AssertFolder(string expected, string? actual) =>
        Assert.True(OpenedExplorer.SameFolder(actual, expected), $"Expected folder '{expected}', got '{actual}'.");

    private static class Win32
    {
        public const int SwMinimize = 6;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(nint hwnd, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(nint hwnd);
    }
}
