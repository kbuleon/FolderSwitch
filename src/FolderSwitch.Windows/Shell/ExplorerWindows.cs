using System.Runtime.InteropServices;

namespace FolderSwitch.Windows.Shell;

/// <summary>
/// The open File Explorer windows, through the documented shell COM interfaces (<c>IShellWindows</c>,
/// <c>IWebBrowser2</c>, <c>IShellBrowser</c>), the same way on Windows 10 and 11.
/// </summary>
/// <remarks>
/// <para>
/// Every method must be called on an STA thread. COM calls go to Explorer's process; they fail rather
/// than throw: a window closed meanwhile, a folder with no view, gives <c>null</c>. They have no
/// timeout: a call waits as long as the Explorer window it reaches does not answer, and enumerating
/// asks every window for its handle, so one hung window blocks <see cref="Enumerate"/>,
/// <see cref="FindActive"/> and <see cref="CurrentFolder"/>. Callers bound that themselves (see
/// <c>CallContextProvider</c>).
/// </para>
/// <para>
/// <c>IShellWindows</c> has one entry per browser: per window on Windows 10, per tab on Windows 11 (the
/// tabs of a window share its top-level HWND). The active tab is the first <c>ShellTabWindowClass</c>
/// child of the Explorer window, compared with the window each tab's <c>IShellBrowser</c> reports.
/// </para>
/// </remarks>
internal static class ExplorerWindows
{
    private const string TabWindowClass = "ShellTabWindowClass";

    /// <summary>
    /// The window of the active tab of an Explorer window: its first <c>ShellTabWindowClass</c> child
    /// (the topmost one). Zero if it has none.
    /// </summary>
    public static nint ActiveTabWindow(nint explorerWindow) => WindowInspector.FindChild(explorerWindow, TabWindowClass);

    /// <summary>
    /// Every open browser (a window, or a tab on Windows 11). The caller disposes them all.
    /// Empty if the shell cannot be reached.
    /// </summary>
    public static List<ExplorerBrowser> Enumerate() => Collect(static _ => true);

    /// <summary>
    /// The browsers of <paramref name="explorerWindow"/>: the window itself on Windows 10, each of its
    /// tabs on Windows 11. The caller disposes them all. Empty if it is not an Explorer window.
    /// </summary>
    public static List<ExplorerBrowser> ForWindow(nint explorerWindow) =>
        explorerWindow == 0 ? [] : Collect(hwnd => hwnd == explorerWindow);

    /// <summary>
    /// The browser of the active tab of <paramref name="explorerWindow"/> (the window itself on
    /// Windows 10), or <c>null</c>. The caller disposes it.
    /// </summary>
    public static ExplorerBrowser? FindActive(nint explorerWindow)
    {
        var candidates = ForWindow(explorerWindow);
        if (candidates.Count == 0)
        {
            return null;
        }

        // One browser: Windows 10, or a single tab. Several: the one whose tab window is the active
        // tab; failing that (should not happen), the first.
        var activeTab = ActiveTabWindow(explorerWindow);
        var chosen = candidates.Find(b => b.TabWindow != 0 && b.TabWindow == activeTab) ?? candidates[0];
        foreach (var other in candidates)
        {
            if (!ReferenceEquals(other, chosen))
            {
                other.Dispose();
            }
        }

        return chosen;
    }

    /// <summary>
    /// The folder shown by the active tab of <paramref name="explorerWindow"/>, as a parsing name (a
    /// path, or <c>::{CLSID}</c> for a virtual location such as This PC), or <c>null</c>.
    /// </summary>
    public static string? CurrentFolder(nint explorerWindow) => CurrentLocation(explorerWindow)?.ParsingName;

    /// <summary>
    /// The folder shown by the active tab of <paramref name="explorerWindow"/>, with the name the shell shows for
    /// it, or <c>null</c>.
    /// </summary>
    public static ShellLocation? CurrentLocation(nint explorerWindow)
    {
        using var browser = FindActive(explorerWindow);
        return browser?.CurrentLocation();
    }

    /// <summary>The browsers whose top-level window satisfies <paramref name="topLevelFilter"/>.</summary>
    private static List<ExplorerBrowser> Collect(Func<nint, bool> topLevelFilter)
    {
        var result = new List<ExplorerBrowser>();
        // Released in finally, whether or not it turns out to be an IShellWindows.
        object? created = null;
        try
        {
            var type = Type.GetTypeFromCLSID(ShellGuids.ShellWindows, throwOnError: false);
            created = type is null ? null : Activator.CreateInstance(type);
            if (created is not IShellWindows windows || windows.get_Count(out var count) != ShellNative.S_OK)
            {
                return result;
            }

            for (var i = 0; i < count; i++)
            {
                if (TryGet(windows, i, topLevelFilter) is { } browser)
                {
                    result.Add(browser);
                }
            }

            return result;
        }
        catch (Exception)
        {
            // Shell unreachable (Explorer restarting...): what was collected so far is still valid.
            return result;
        }
        finally
        {
            ShellNative.Release(created);
        }
    }

    private static ExplorerBrowser? TryGet(IShellWindows windows, int index, Func<nint, bool> topLevelFilter)
    {
        object? item = null;
        object? shellBrowser = null;
        try
        {
            if (windows.Item(index, out item) != ShellNative.S_OK
                || item is not IWebBrowser2 webBrowser
                || webBrowser.get_HWND(out var topLevel) != ShellNative.S_OK
                || !topLevelFilter(topLevel))
            {
                return null;
            }

            nint tab = 0;
            if (item is IComServiceProvider services
                && services.QueryService(ShellGuids.TopLevelBrowser, typeof(IShellBrowser).GUID, out shellBrowser)
                    == ShellNative.S_OK
                && shellBrowser is IShellBrowser browser
                && browser.GetWindow(out var window) == ShellNative.S_OK)
            {
                tab = window;
            }

            // A service that is not an IShellBrowser after all is released in finally.
            var result = new ExplorerBrowser(webBrowser, shellBrowser as IShellBrowser, topLevel, tab);
            item = null;
            if (shellBrowser is IShellBrowser)
            {
                shellBrowser = null;
            }

            return result;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            ShellNative.Release(item);
            ShellNative.Release(shellBrowser);
        }
    }
}

/// <summary>A location of the shell: its parsing name, and the name the shell shows for it when it has one.</summary>
internal sealed record ShellLocation(string ParsingName, string? DisplayName);

/// <summary>
/// One Explorer browser (a window, or a tab on Windows 11), holding its COM objects until disposed.
/// Use it on the STA thread that obtained it.
/// </summary>
internal sealed class ExplorerBrowser : IDisposable
{
    private static readonly Guid PersistFolder2Iid = typeof(IPersistFolder2).GUID;

    /// <summary><c>SBSP_SAMEBROWSER</c>: in this browser, never a new window.</summary>
    private const uint SameBrowser = 0x0001;

    /// <summary><c>SBSP_ABSOLUTE</c>: the ID list is absolute.</summary>
    private const uint Absolute = 0x0000;

    private IWebBrowser2? _webBrowser;
    private IShellBrowser? _shellBrowser;

    internal ExplorerBrowser(IWebBrowser2 webBrowser, IShellBrowser? shellBrowser, nint topLevelWindow, nint tabWindow)
    {
        _webBrowser = webBrowser;
        _shellBrowser = shellBrowser;
        TopLevelWindow = topLevelWindow;
        TabWindow = tabWindow;
    }

    /// <summary>The Explorer window (<c>CabinetWClass</c>) the browser belongs to.</summary>
    public nint TopLevelWindow { get; }

    /// <summary>
    /// The window <c>IShellBrowser</c> reports for the browser: its <c>ShellTabWindowClass</c> tab
    /// window on Windows 11. Zero if unknown.
    /// </summary>
    public nint TabWindow { get; }

    /// <summary>The browser automation object (navigation, closing). Valid until disposed.</summary>
    public IWebBrowser2 WebBrowser => _webBrowser ?? throw new ObjectDisposedException(nameof(ExplorerBrowser));

    /// <summary>
    /// The folder shown, as a parsing name (<c>SIGDN_DESKTOPABSOLUTEPARSING</c>: a path, or
    /// <c>::{CLSID}</c> for a virtual location), read from the active shell view; <c>null</c> if unknown.
    /// </summary>
    public string? CurrentFolder() => CurrentLocation()?.ParsingName;

    /// <summary>
    /// The folder shown, as a parsing name (see <see cref="CurrentFolder"/>) with the name the shell shows for it
    /// (<c>SIGDN_NORMALDISPLAY</c>, <c>null</c> if it has none); <c>null</c> if unknown.
    /// </summary>
    public ShellLocation? CurrentLocation()
    {
        if (_shellBrowser is not { } browser)
        {
            return null;
        }

        object? view = null;
        object? folder = null;
        var pidl = nint.Zero;
        try
        {
            if (browser.QueryActiveShellView(out view) != ShellNative.S_OK
                || view is not IFolderView folderView
                || folderView.GetFolder(PersistFolder2Iid, out folder) != ShellNative.S_OK
                || folder is not IPersistFolder2 persist
                || persist.GetCurFolder(out pidl) != ShellNative.S_OK)
            {
                return null;
            }

            return ShellNative.ParsingName(pidl) is { } parsingName
                ? new ShellLocation(parsingName, ShellNative.DisplayName(pidl))
                : null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (pidl != 0)
            {
                Marshal.FreeCoTaskMem(pidl);
            }

            ShellNative.Release(folder);
            ShellNative.Release(view);
        }
    }

    /// <summary>
    /// Navigates this browser itself to the absolute ID list <paramref name="idList"/>
    /// (<c>IShellBrowser.BrowseObject</c> with <c>SBSP_SAMEBROWSER | SBSP_ABSOLUTE</c>); <c>false</c> if it has no
    /// <c>IShellBrowser</c> or refused.
    /// </summary>
    public bool BrowseHere(byte[] idList)
    {
        if (_shellBrowser is not { } browser)
        {
            return false;
        }

        var pidl = Marshal.AllocCoTaskMem(idList.Length);
        try
        {
            Marshal.Copy(idList, 0, pidl, idList.Length);
            browser.BrowseObject(pidl, SameBrowser | Absolute);
            return true;
        }
        catch (Exception)
        {
            // Refused (a tab not ready yet), or the browser is gone.
            return false;
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    public void Dispose()
    {
        ShellNative.Release(_shellBrowser);
        ShellNative.Release(_webBrowser);
        _shellBrowser = null;
        _webBrowser = null;
    }
}
