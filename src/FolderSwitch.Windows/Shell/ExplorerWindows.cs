// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using FolderSwitch.Core.Services;

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
/// <see cref="FindActive"/>, <see cref="CurrentFolder"/> and <see cref="Selection"/>. Callers bound that themselves (see
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

    /// <summary>
    /// The file selected in the active tab of <paramref name="explorerWindow"/> (see <see cref="ExplorerBrowser.Selection"/>),
    /// or <c>null</c> if it could not be read.
    /// </summary>
    public static ExplorerSelection? Selection(nint explorerWindow)
    {
        using var browser = FindActive(explorerWindow);
        return browser?.Selection();
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
    private static readonly Guid ShellItemArrayIid = typeof(IShellItemArray).GUID;

    /// <summary><c>SBSP_SAMEBROWSER</c>: in this browser, never a new window.</summary>
    private const uint SameBrowser = 0x0001;

    /// <summary><c>SBSP_ABSOLUTE</c>: the ID list is absolute.</summary>
    private const uint Absolute = 0x0000;

    /// <summary><c>SVGIO_SELECTION</c>: the selected items of a view.</summary>
    private const uint SelectedItems = 0x0001;

    /// <summary>The attributes that tell what the selected item is.</summary>
    private const uint SelectionAttributes =
        ShellNative.SfgaoFolder | ShellNative.SfgaoStream | ShellNative.SfgaoFileSystem | ShellNative.SfgaoLink;

    /// <summary><c>STGM_READ</c>.</summary>
    private const uint ReadOnly = 0x0000;

    /// <summary>The room for a shortcut's target path, in characters: the longest path, its terminator included.</summary>
    private const int TargetPathLength = 32768;

    /// <summary>The parsing name of the Recycle Bin.</summary>
    private const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";

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
    /// The file selected in the folder shown, read from the active shell view: the one selected item when it is a file,
    /// or a shortcut's target when that is an existing file; <see cref="ExplorerSelection.NoFile"/> when there is none
    /// to offer (see <see cref="ExplorerSelection"/>); <c>null</c> if it could not be read. A shortcut is only read from
    /// its file, never resolved: no search for a moved target, no UI.
    /// </summary>
    public ExplorerSelection? Selection()
    {
        if (_shellBrowser is not { } browser)
        {
            return null;
        }

        object? view = null;
        object? folder = null;
        object? items = null;
        IShellItem? item = null;
        var pidl = nint.Zero;
        try
        {
            if (browser.QueryActiveShellView(out view) != ShellNative.S_OK
                || view is not IFolderView folderView
                || folderView.ItemCount(SelectedItems, out var count) != ShellNative.S_OK)
            {
                return null;
            }

            if (count != 1)
            {
                return ExplorerSelection.NoFile;
            }

            // The items of the Recycle Bin have a file-system path (where the bin keeps them): none is offered.
            if (folderView.GetFolder(PersistFolder2Iid, out folder) != ShellNative.S_OK
                || folder is not IPersistFolder2 persist
                || persist.GetCurFolder(out pidl) != ShellNative.S_OK
                || ShellNative.ParsingName(pidl) is not { } folderName)
            {
                return null;
            }

            if (string.Equals(folderName, RecycleBin, StringComparison.OrdinalIgnoreCase))
            {
                return ExplorerSelection.NoFile;
            }

            // The selection may have changed since it was counted: the items read now are the answer.
            if (folderView.Items(SelectedItems, ShellItemArrayIid, out items) != ShellNative.S_OK
                || items is not IShellItemArray array
                || array.GetCount(out var selected) != ShellNative.S_OK)
            {
                return null;
            }

            if (selected != 1)
            {
                return ExplorerSelection.NoFile;
            }

            return array.GetItemAt(0, out item) == ShellNative.S_OK && item is not null ? FileOf(item) : null;
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

            ShellNative.Release(item);
            ShellNative.Release(items);
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

    /// <summary>What the one selected <paramref name="item"/> offers; <c>null</c> if it could not be read.</summary>
    private static ExplorerSelection? FileOf(IShellItem item)
    {
        if (item.GetAttributes(SelectionAttributes, out var attributes) < 0)
        {
            return null;
        }

        var result = item.GetDisplayName(ShellNative.SigdnFileSysPath, out var name);
        var path = ShellNative.TakeString(name);
        if (result != ShellNative.S_OK || string.IsNullOrEmpty(path))
        {
            // An item outside the file system (This PC, a library...) has no path: nothing to offer. A file-system item
            // that did not give one was not read.
            return (attributes & ShellNative.SfgaoFileSystem) == 0 ? ExplorerSelection.NoFile : null;
        }

        // A shortcut counts by its target; one that is not a shell link (an Internet shortcut) is a plain file.
        if ((attributes & ShellNative.SfgaoLink) != 0 && ShortcutTarget(path) is { } target)
        {
            return target;
        }

        // A folder is not a file; a .zip, a folder that has a stream, is one.
        return (attributes & (ShellNative.SfgaoFolder | ShellNative.SfgaoStream)) == ShellNative.SfgaoFolder
            ? ExplorerSelection.NoFile
            : new ExplorerSelection(path);
    }

    /// <summary>
    /// What the shortcut <paramref name="path"/> offers: its target when that is an existing file, else
    /// <see cref="ExplorerSelection.NoFile"/> (a folder, a missing target, a target with no path, a <c>.lnk</c> that does
    /// not load: damaged, denied, not available offline); <c>null</c> when another kind of shortcut does not load as a shell
    /// link (an Internet shortcut...). Read from the file (<c>STGM_READ</c>) in this process, never resolved. Throws if no
    /// shell link object can be made.
    /// </summary>
    private static ExplorerSelection? ShortcutTarget(string path)
    {
        object? link = null;
        var buffer = nint.Zero;
        try
        {
            link = Activator.CreateInstance(Type.GetTypeFromCLSID(ShellGuids.ShellLink, throwOnError: true)!);
            if (((IPersistFile)link!).Load(path, ReadOnly) < 0)
            {
                // A .lnk is a shell link whatever its content: its target could not be read.
                return string.Equals(Path.GetExtension(path), ".lnk", StringComparison.OrdinalIgnoreCase)
                    ? ExplorerSelection.NoFile
                    : null;
            }

            buffer = Marshal.AllocCoTaskMem(TargetPathLength * sizeof(char));
            var target = ((IShellLinkW)link).GetPath(buffer, TargetPathLength, 0, 0) == ShellNative.S_OK
                ? Marshal.PtrToStringUni(buffer)
                : null;

            // File.Exists is false for a folder, a missing file and a file it cannot reach.
            return !string.IsNullOrEmpty(target) && File.Exists(target)
                ? new ExplorerSelection(target)
                : ExplorerSelection.NoFile;
        }
        finally
        {
            if (buffer != 0)
            {
                Marshal.FreeCoTaskMem(buffer);
            }

            ShellNative.Release(link);
        }
    }
}
