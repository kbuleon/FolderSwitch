// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using FolderSwitch.Core.Services;
using FolderSwitch.Windows.SystemServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FolderSwitch.Windows.Shell;

/// <summary>
/// Drives File Explorer windows: navigates the active tab of a window (<see cref="ExplorerWindows"/>,
/// <c>IWebBrowser2.Navigate2</c>), opens a new tab on Windows 11, opens a new window
/// (<c>ShellExecuteEx</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Locations.</b> Every location (disk path, UNC path, <c>shell:</c> or <c>::{CLSID}</c> form) is
/// parsed here into an ID list (<c>SHParseDisplayName</c>), which <c>Navigate2</c> is documented to take
/// as a <c>VT_ARRAY | VT_UI1</c> variant. Not the string itself: <c>Navigate2</c> treats a string as an
/// address, and for a UNC path (<c>\\localhost\C$</c>) it held Explorer's window for 15 to 60 s before
/// failing with <c>ERROR_CANCELLED</c>, where the ID list navigates at once. Parsing here also puts the
/// slow part (a network path) on the controller's thread, within its budget, rather than on Explorer's
/// window thread, and a location that does not parse fails before anything is asked of Explorer (in
/// particular before a new tab is opened). A new tab takes the same ID list through
/// <c>IShellBrowser.BrowseObject</c> instead (see below).
/// </para>
/// <para>
/// <b>Threads.</b> Any thread, STA (the UI thread) or MTA; calls are serialized. The COM work runs on an
/// STA thread owned by the controller, which the caller waits for at most <see cref="DefaultBudget"/>
/// (3 s): a COM call into Explorer waits as long as the window it reaches does not answer (a window hung
/// on an offline share), and the caller is normally the UI thread.
/// </para>
/// <para>
/// <b>Overruns.</b> As in <c>CallContextProvider</c>: work that overruns is abandoned with its thread,
/// which ends when the work returns (a navigation already in flight may then still happen late; a new
/// tab is not navigated any more, see below). At most one abandoned
/// thread is kept: while it is alive, <see cref="Navigate"/> and <see cref="OpenInNewTab"/> return
/// <c>false</c> at once rather than start another thread that would block the same way.
/// </para>
/// <para>
/// <b>Foreground.</b> Once the navigation is issued, the window is restored if minimized and brought
/// to the foreground, from the calling thread, with calls that do not wait for the window
/// (<c>ShowWindowAsync</c>, <c>SetForegroundWindow</c>). Windows allows it when the calling process is
/// the foreground one, as FolderSwitch is when its item is clicked; otherwise the window's taskbar
/// button flashes. Best effort: it does not change the result.
/// </para>
/// <para>
/// <b>New tab (Windows 11).</b> Undocumented: <c>WM_COMMAND 0xA21B</c>, the command of the tab bar's
/// "+" button, posted to the active tab window. The new tab is told apart in <c>IShellWindows</c> as a
/// browser of the same window whose tab window was not there before, and navigated with
/// <c>IShellBrowser.BrowseObject</c> and <c>SBSP_SAMEBROWSER</c>, not <c>Navigate2</c>: Explorer carries out
/// a <c>Navigate2</c> made while Ctrl is held down in a new window of its own, and the user still holds the
/// Ctrl of their Ctrl+click a moment after it (the tab then stayed on its start page, beside a new window).
/// If it does not appear within
/// <see cref="NewTabTimeout"/>, the call fails; if it appears but does not take the navigation within
/// <see cref="NewTabNavigationTimeout"/>, it is closed and the call fails. If the call overran meanwhile
/// (the caller then falls back to a new window), the work, still running, closes the tab when it appears
/// instead of navigating it. A tab that appears only after the work's own timeout is left open.
/// </para>
/// <para>
/// <b>New window.</b> <c>ShellExecuteEx</c> with the "open" verb, on the calling thread when it is an STA
/// thread (see <see cref="ShellLauncher"/>), without the shell's error message box: the failure is
/// thrown. Not bounded: it starts a new window rather than driving an existing one.
/// </para>
/// </remarks>
public sealed class ExplorerController : IExplorerController, IDisposable
{
    /// <summary>The time the COM work of <see cref="Navigate"/> or <see cref="OpenInNewTab"/> may take.</summary>
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(3);

    /// <summary>How long a new tab may take to appear in <c>IShellWindows</c>.</summary>
    internal static readonly TimeSpan NewTabTimeout = TimeSpan.FromSeconds(1);

    /// <summary>How long a new tab, once there, may refuse the navigation (it is not ready at once).</summary>
    internal static readonly TimeSpan NewTabNavigationTimeout = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(25);

    /// <summary>The <c>WM_COMMAND</c> identifier of the "+" button of Explorer's tab bar (undocumented).</summary>
    private const uint NewTabCommand = 0xA21B;

    private const string WorkerName = "FolderSwitch Explorer control";

    private readonly TimeSpan _budget;
    private readonly bool _tabsSupported;
    private readonly Lock _gate = new();
    private StaWorker? _worker;

    /// <summary>The worker given up on after an overrun, while it may still be running its work.</summary>
    private StaWorker? _abandoned;
    private bool _disposed;

    public ExplorerController()
        : this(DefaultBudget, WindowsVersion.IsWindows11)
    {
    }

    /// <summary>
    /// Test seam: another time budget, and whether Explorer has tabs (<c>false</c>: Windows 10, where
    /// <see cref="OpenInNewTab"/> does nothing).
    /// </summary>
    internal ExplorerController(TimeSpan budget, bool tabsSupported)
    {
        _budget = budget;
        _tabsSupported = tabsSupported;
    }

    /// <summary>Test seam: run on the worker thread right after the new-tab command is posted.</summary>
    internal Action? AfterNewTabCommand { get; init; }

    /// <inheritdoc/>
    /// <returns>
    /// <c>true</c> once the navigation is issued (not completed); <c>false</c> if the window is not an
    /// Explorer window, the location does not parse (it does not exist), Explorer refused it, or the
    /// work did not finish in time.
    /// </returns>
    public bool Navigate(nint explorerWindow, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (explorerWindow == 0 || !RunBounded(_ => NavigateActiveTab(explorerWindow, path)))
        {
            return false;
        }

        BringToForeground(explorerWindow);
        return true;
    }

    /// <inheritdoc/>
    /// <returns>
    /// <c>true</c> once the new tab exists and its navigation is issued; <c>false</c> on Windows 10
    /// (nothing done), or if the tab did not appear or did not take the navigation in time.
    /// </returns>
    public bool OpenInNewTab(nint explorerWindow, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var afterCommand = AfterNewTabCommand;
        if (!_tabsSupported
            || explorerWindow == 0
            || !RunBounded(givenUp => OpenTabAndNavigate(explorerWindow, path, givenUp, afterCommand)))
        {
            return false;
        }

        BringToForeground(explorerWindow);
        return true;
    }

    /// <inheritdoc/>
    /// <exception cref="System.ComponentModel.Win32Exception">The shell could not open the location.</exception>
    public void OpenNewWindow(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ShellLauncher.Execute(path, "open", showErrors: false);
    }

    /// <summary>Stops the worker thread. Later navigations return <c>false</c>; <see cref="OpenNewWindow"/> still works.</summary>
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
    /// given up, so work that goes on must not take effect any more. Internal for tests.
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

    private static bool NavigateActiveTab(nint explorerWindow, string path)
    {
        if (Location(path) is not { } location)
        {
            return false;
        }

        using var browser = ExplorerWindows.FindActive(explorerWindow);
        return browser is not null && TryNavigate(browser, location);
    }

    private static bool OpenTabAndNavigate(nint explorerWindow, string path, CancellationToken givenUp, Action? afterCommand)
    {
        var activeTab = ExplorerWindows.ActiveTabWindow(explorerWindow);
        if (Location(path) is not { } location || activeTab == 0)
        {
            return false;
        }

        var before = TabWindows(explorerWindow);
        if (before.Count == 0
            || givenUp.IsCancellationRequested
            || !PInvoke.PostMessage((HWND)activeTab, PInvoke.WM_COMMAND, (WPARAM)(nuint)NewTabCommand, default))
        {
            return false;
        }

        afterCommand?.Invoke();
        var start = Stopwatch.GetTimestamp();
        nint added = 0;
        while (true)
        {
            Thread.Sleep(PollInterval);
            var browsers = ExplorerWindows.ForWindow(explorerWindow);
            try
            {
                var tab = added == 0
                    ? browsers.Find(b => b.TabWindow != 0 && !before.Contains(b.TabWindow))
                    : browsers.Find(b => b.TabWindow == added);
                if (tab is not null)
                {
                    added = tab.TabWindow;
                    if (givenUp.IsCancellationRequested)
                    {
                        // The caller gave up meanwhile and fell back to a new window: no second one here.
                        Quit(tab);
                        return false;
                    }

                    if (tab.BrowseHere(location))
                    {
                        return true;
                    }
                }

                var elapsed = Stopwatch.GetElapsedTime(start);
                if (added == 0 ? elapsed >= NewTabTimeout : elapsed >= NewTabTimeout + NewTabNavigationTimeout)
                {
                    // A tab that would not navigate is closed: the caller falls back to a new window.
                    if (tab is not null)
                    {
                        Quit(tab);
                    }

                    return false;
                }
            }
            finally
            {
                browsers.ForEach(b => b.Dispose());
            }
        }
    }

    /// <summary>What <c>Navigate2</c> is given: the ID list of <paramref name="path"/> as bytes; <c>null</c> if it does not parse.</summary>
    private static byte[]? Location(string path) => ShellNative.IdListBytes(path);

    private static bool TryNavigate(ExplorerBrowser browser, byte[] location)
    {
        try
        {
            return browser.WebBrowser.Navigate2(location, null!, null!, null!, null!) == ShellNative.S_OK;
        }
        catch (Exception)
        {
            // Browser gone meanwhile.
            return false;
        }
    }

    /// <summary>The tab windows of the browsers of <paramref name="explorerWindow"/> (empty: not an Explorer window).</summary>
    private static HashSet<nint> TabWindows(nint explorerWindow)
    {
        var browsers = ExplorerWindows.ForWindow(explorerWindow);
        try
        {
            return browsers.Select(b => b.TabWindow).ToHashSet();
        }
        finally
        {
            browsers.ForEach(b => b.Dispose());
        }
    }

    private static void Quit(ExplorerBrowser browser)
    {
        try
        {
            _ = browser.WebBrowser.Quit();
        }
        catch (Exception)
        {
            // Already gone.
        }
    }

    // ---- On the calling thread ----

    private static void BringToForeground(nint explorerWindow)
    {
        var window = (HWND)explorerWindow;
        if (PInvoke.IsIconic(window))
        {
            _ = PInvoke.ShowWindowAsync(window, SHOW_WINDOW_CMD.SW_RESTORE);
        }

        _ = PInvoke.SetForegroundWindow(window);
    }
}
