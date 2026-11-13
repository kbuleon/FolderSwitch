// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using FolderSwitch.Windows.Shell;

namespace FolderSwitch.Windows.IntegrationTests.Support;

/// <summary>
/// Sets the selection in the view of an <see cref="OpenedExplorer"/> through its <c>IShellView.SelectItem</c> (no
/// keystroke, no click, the window may stay in the background), then waits until the view reports that many selected
/// items. A view still filling may not know an item yet: the selection is set again until it holds.
/// </summary>
internal static class ExplorerViewSelection
{
    /// <summary><c>SVSI_DESELECT</c>.</summary>
    private const uint Deselect = 0x0000;

    /// <summary><c>SVSI_SELECT</c>.</summary>
    private const uint Select = 0x0001;

    /// <summary><c>SVSI_DESELECTOTHERS</c>.</summary>
    private const uint DeselectOthers = 0x0004;

    /// <summary><c>SVGIO_SELECTION</c>.</summary>
    private const uint SelectedItems = 0x0001;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Selects exactly <paramref name="paths"/>, items of the folder the browser shows.</summary>
    public static void SelectOnly(OpenedExplorer explorer, params string[] paths) =>
        Apply(explorer, [.. paths.Select((path, i) => (path, i == 0 ? Select | DeselectOthers : Select))], paths.Length);

    /// <summary>Clears the selection, by way of <paramref name="anyItem"/>, an item of the folder the browser shows.</summary>
    public static void Clear(OpenedExplorer explorer, string anyItem) =>
        Apply(explorer, [(anyItem, Deselect | DeselectOthers)], 0);

    private static void Apply(OpenedExplorer explorer, (string Path, uint Flags)[] calls, int expected)
    {
        var deadline = DateTime.UtcNow + Timeout;
        int? count;
        while ((count = Sta.Run(() => TryApply(explorer, calls))) != expected && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
        }

        Assert.True(count == expected, $"The view reports {count?.ToString() ?? "no"} selected items, not {expected}.");
    }

    /// <summary>One attempt, on an STA thread: the number of items the view reports selected afterwards, or <c>null</c>.</summary>
    private static int? TryApply(OpenedExplorer explorer, (string Path, uint Flags)[] calls)
    {
        var browsers = ExplorerWindows.Enumerate();
        object? service = null;
        object? view = null;
        try
        {
            var mine = browsers.Find(b => b.TopLevelWindow == explorer.Window && b.TabWindow == explorer.Tab);
            if (mine?.WebBrowser is not IComServiceProvider services
                || services.QueryService(ShellGuids.TopLevelBrowser, typeof(IShellBrowser).GUID, out service) != ShellNative.S_OK
                || service is not IShellBrowser browser
                || browser.QueryActiveShellView(out view) != ShellNative.S_OK
                || view is not IShellView shellView
                || view is not IFolderView folderView)
            {
                return null;
            }

            foreach (var (path, flags) in calls)
            {
                var parsed = ShellNative.SHParseDisplayName(path, 0, out var pidl, 0, out _);
                try
                {
                    if (parsed != ShellNative.S_OK || shellView.SelectItem(ILFindLastID(pidl), flags) < 0)
                    {
                        return null;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pidl);
                }
            }

            return folderView.ItemCount(SelectedItems, out var count) == ShellNative.S_OK ? count : null;
        }
        finally
        {
            ShellNative.Release(view);
            ShellNative.Release(service);
            browsers.ForEach(b => b.Dispose());
        }
    }

    /// <summary>The last item of an ID list: a child ID list within it.</summary>
    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern nint ILFindLastID(nint pidl);
}

/// <summary><c>IShellView</c> (with the <c>IOleWindow</c> methods it inherits), up to <c>SelectItem</c>.</summary>
[ComImport]
[Guid("000214E3-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellView
{
    void GetWindow(out nint hwnd);

    void ContextSensitiveHelp(int enterMode);

    void TranslateAccelerator(nint message);

    void EnableModeless(int enable);

    void UIActivate(uint state);

    void Refresh();

    void CreateViewWindow(nint previous, nint settings, nint browser, nint rectangle, out nint hwnd);

    void DestroyViewWindow();

    void GetCurrentInfo(nint settings);

    void AddPropertySheetPages(uint reserved, nint callback, nint parameter);

    void SaveViewState();

    /// <summary>Changes the selection state of the child item <paramref name="pidlItem"/> (<c>SVSI_*</c> flags).</summary>
    [PreserveSig]
    int SelectItem(nint pidlItem, uint flags);
}
