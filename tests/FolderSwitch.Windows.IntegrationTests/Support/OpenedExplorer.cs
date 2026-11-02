// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using System.Runtime.InteropServices;
using FolderSwitch.Windows.Shell;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>
/// An Explorer browser opened by a test (with <c>explorer.exe &lt;location&gt;</c>, or any other action):
/// a new window, or a new tab of an existing window when Windows 11 is set to open folders in tabs. It is found in
/// <c>IShellWindows</c> as the browser that was not there before and shows the expected folder.
/// Dispose closes that browser only (<c>IWebBrowser2.Quit</c>: the window, or just the tab).
/// </summary>
internal sealed class OpenedExplorer : IDisposable
{
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(10);

    private OpenedExplorer(nint window, nint tab, bool isNewWindow)
    {
        Window = window;
        Tab = tab;
        IsNewWindow = isNewWindow;
    }

    /// <summary>The Explorer window (<c>CabinetWClass</c>).</summary>
    public nint Window { get; }

    /// <summary>The browser's tab window (as <c>IShellBrowser</c> reports it).</summary>
    public nint Tab { get; }

    /// <summary>Whether the browser came in a window of its own (rather than a tab of an existing one).</summary>
    public bool IsNewWindow { get; }

    /// <summary>Opens <paramref name="location"/> with <c>explorer.exe</c> and waits for the browser showing <paramref name="expectedFolder"/>.</summary>
    public static OpenedExplorer Open(string location, string expectedFolder) => OpenBy(
        () =>
        {
            var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            info.ArgumentList.Add(location);
            using (Process.Start(info))
            {
                // explorer.exe hands the request to the running shell and exits.
            }
        },
        expectedFolder);

    /// <summary>
    /// Runs <paramref name="open"/>, then waits for the new browser showing <paramref name="expectedFolder"/>.
    /// If none comes, the browsers that did appear meanwhile are closed and the test fails.
    /// </summary>
    public static OpenedExplorer OpenBy(Action open, string expectedFolder)
    {
        var before = Snapshot();
        open();

        if (WaitForNew(before, expectedFolder, OpenTimeout) is { } opened)
        {
            return opened;
        }

        var seen = string.Join(", ", NewFolders(before, 0));
        CloseNew(before, 0);
        Assert.Fail($"No new Explorer browser showed '{expectedFolder}' within {OpenTimeout.TotalSeconds} s (new browsers seen, closed: [{seen}]).");
        return null!;
    }

    /// <summary>The browsers open now, as (window, tab window).</summary>
    public static HashSet<(nint Window, nint Tab)> Snapshot() => Sta.Run(Browsers);

    /// <summary>
    /// The first browser not in <paramref name="before"/> that shows <paramref name="expectedFolder"/>
    /// (only in <paramref name="window"/> if not zero), or <c>null</c> after <paramref name="timeout"/>.
    /// </summary>
    public static OpenedExplorer? WaitForNew(HashSet<(nint Window, nint Tab)> before, string expectedFolder, TimeSpan timeout, nint window = 0)
    {
        var windowsBefore = before.Select(b => b.Window).ToHashSet();
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var found = Sta.Run(() =>
            {
                var browsers = ExplorerWindows.Enumerate();
                try
                {
                    return browsers
                        .Where(b => (window == 0 || b.TopLevelWindow == window) && !before.Contains((b.TopLevelWindow, b.TabWindow)))
                        .Where(b => SameFolder(b.CurrentFolder(), expectedFolder))
                        .Select(b => ((nint Window, nint Tab)?)(b.TopLevelWindow, b.TabWindow))
                        .FirstOrDefault();
                }
                finally
                {
                    browsers.ForEach(b => b.Dispose());
                }
            });

            if (found is { } browser)
            {
                return new OpenedExplorer(browser.Window, browser.Tab, !windowsBefore.Contains(browser.Window));
            }

            if (DateTime.UtcNow >= deadline)
            {
                return null;
            }

            Thread.Sleep(200);
        }
    }

    /// <summary>The folders of the browsers not in <paramref name="before"/> (only in <paramref name="window"/> if not zero).</summary>
    public static List<string> NewFolders(HashSet<(nint Window, nint Tab)> before, nint window) => Sta.Run(() =>
    {
        var browsers = ExplorerWindows.Enumerate();
        try
        {
            return browsers
                .Where(b => (window == 0 || b.TopLevelWindow == window) && !before.Contains((b.TopLevelWindow, b.TabWindow)))
                .Select(b => b.CurrentFolder() ?? "?")
                .ToList();
        }
        finally
        {
            browsers.ForEach(b => b.Dispose());
        }
    });

    /// <summary>
    /// Closes the browsers not in <paramref name="before"/> (only in <paramref name="window"/> if not
    /// zero): cleanup when what a test opened could not be identified.
    /// </summary>
    public static void CloseNew(HashSet<(nint Window, nint Tab)> before, nint window)
    {
        var deadline = DateTime.UtcNow + CloseTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var remaining = Sta.Run(() =>
            {
                var browsers = ExplorerWindows.Enumerate();
                try
                {
                    var strays = browsers
                        .Where(b => (window == 0 || b.TopLevelWindow == window) && !before.Contains((b.TopLevelWindow, b.TabWindow)))
                        .ToList();
                    strays.ForEach(b => b.WebBrowser.Quit());
                    return strays.Count;
                }
                finally
                {
                    browsers.ForEach(b => b.Dispose());
                }
            });

            if (remaining == 0)
            {
                return;
            }

            Thread.Sleep(200);
        }
    }

    /// <summary>The folder this browser shows now, or <c>null</c> if it is gone.</summary>
    public string? CurrentFolder() => Sta.Run(() =>
    {
        var browsers = ExplorerWindows.Enumerate();
        try
        {
            return browsers.Find(b => b.TopLevelWindow == Window && b.TabWindow == Tab)?.CurrentFolder();
        }
        finally
        {
            browsers.ForEach(b => b.Dispose());
        }
    });

    /// <summary>Polls <see cref="CurrentFolder"/> until it is <paramref name="expected"/>; the last folder read.</summary>
    public string? WaitForFolder(string expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var current = CurrentFolder();
            if (SameFolder(current, expected) || DateTime.UtcNow >= deadline)
            {
                return current;
            }

            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Opens a new tab in <paramref name="window"/> (Windows 11) with the command Explorer's "+" button
    /// sends (<c>WM_COMMAND 0xA21B</c> to the active tab window: posted, not a keystroke), then
    /// navigates it to <paramref name="folder"/> with <c>IWebBrowser2.Navigate2</c>.
    /// </summary>
    public static OpenedExplorer OpenTab(OpenedExplorer window, string folder)
    {
        const uint wmCommand = 0x0111;
        const int newTabCommand = 0xA21B;

        var before = Sta.Run(Browsers);
        Assert.True(PostMessage(ExplorerWindows.ActiveTabWindow(window.Window), wmCommand, newTabCommand, 0));

        var deadline = DateTime.UtcNow + OpenTimeout;
        nint tab = 0;
        while (tab == 0 && DateTime.UtcNow < deadline)
        {
            tab = Sta.Run(() =>
            {
                var browsers = ExplorerWindows.Enumerate();
                try
                {
                    var added = browsers.Find(b => b.TopLevelWindow == window.Window && !before.Contains((b.TopLevelWindow, b.TabWindow)));
                    if (added is null)
                    {
                        return 0;
                    }

                    // A tab that has just appeared may not take a navigation yet: tried again.
                    return added.WebBrowser.Navigate2(folder, null!, null!, null!, null!) == 0 ? added.TabWindow : 0;
                }
                finally
                {
                    browsers.ForEach(b => b.Dispose());
                }
            });

            if (tab == 0)
            {
                Thread.Sleep(100);
            }
        }

        Assert.True(tab != 0, "No new tab appeared.");
        var opened = new OpenedExplorer(window.Window, tab, isNewWindow: false);
        while (DateTime.UtcNow < deadline)
        {
            var current = Sta.Run(() =>
            {
                var browsers = ExplorerWindows.Enumerate();
                try
                {
                    return browsers.Find(b => b.TabWindow == tab)?.CurrentFolder();
                }
                finally
                {
                    browsers.ForEach(b => b.Dispose());
                }
            });

            if (SameFolder(current, folder))
            {
                return opened;
            }

            Thread.Sleep(100);
        }

        opened.Dispose();
        Assert.Fail($"The new tab did not reach '{folder}'.");
        return null!;
    }

    public static bool SameFolder(string? actual, string expected) =>
        actual is not null
        && string.Equals(actual.TrimEnd('\\'), expected.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        var deadline = DateTime.UtcNow + CloseTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var stillOpen = Sta.Run(() =>
            {
                var browsers = ExplorerWindows.Enumerate();
                try
                {
                    var mine = browsers.Find(b => b.TopLevelWindow == Window && b.TabWindow == Tab);
                    _ = mine?.WebBrowser.Quit();
                    return mine is not null;
                }
                finally
                {
                    browsers.ForEach(b => b.Dispose());
                }
            });

            if (!stillOpen)
            {
                return;
            }

            Thread.Sleep(200);
        }
    }

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);

    private static HashSet<(nint Window, nint Tab)> Browsers()
    {
        var browsers = ExplorerWindows.Enumerate();
        try
        {
            return browsers.Select(b => (b.TopLevelWindow, b.TabWindow)).ToHashSet();
        }
        finally
        {
            browsers.ForEach(b => b.Dispose());
        }
    }
}
