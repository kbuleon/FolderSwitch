using System.Runtime.InteropServices;
using FolderSwitch.Windows.Shell;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>Window trees built in the test process with hidden <c>STATIC</c> windows (on the test thread, which owns them).</summary>
[Trait("Category", "Desktop")]
public sealed class WindowInspectorTests
{
    [Fact]
    public void Snapshot_reads_class_process_and_descendants_breadth_first()
    {
        using var tree = HiddenWindows.Create();
        var root = tree.Add(0, "FolderSwitch test root");
        var child = tree.Add(root, id: 0x10);
        tree.Add(child, id: 0x20);
        tree.Add(root, id: 0x11);

        var info = WindowInspector.Snapshot(root);

        Assert.Equal("Static", info.ClassName);
        Assert.Equal(Environment.ProcessPath is { } path ? Path.GetFileNameWithoutExtension(path) : "", info.ProcessName);
        Assert.Equal(["Static", "Static", "Static"], info.DescendantClassNames);
        Assert.Equal([0x10, 0x11, 0x20], info.DescendantControlIds);
    }

    [Fact]
    public void Snapshot_stops_six_levels_down()
    {
        using var tree = HiddenWindows.Create();
        var root = tree.Add(0, "FolderSwitch test root");
        var parent = root;
        for (var depth = 1; depth <= 8; depth++)
        {
            parent = tree.Add(parent, id: depth);
        }

        var info = WindowInspector.Snapshot(root);

        Assert.Equal([1, 2, 3, 4, 5, 6], info.DescendantControlIds);
    }

    [Fact]
    public void Snapshot_lists_at_most_300_windows_shallow_ones_first()
    {
        using var tree = HiddenWindows.Create();
        var root = tree.Add(0, "FolderSwitch test root");
        var first = tree.Add(root, id: 1);
        for (var i = 0; i < 320; i++)
        {
            tree.Add(first, id: 1000 + i);
        }

        // Created last, but one level up: listed before the 320 grandchildren.
        tree.Add(root, id: 2);

        var info = WindowInspector.Snapshot(root);

        Assert.Equal(WindowInspector.MaxWindows, info.DescendantClassNames.Count);
        Assert.Equal([1, 2], info.DescendantControlIds.Take(2));
    }

    [Fact]
    public void Snapshot_of_a_window_that_is_gone_is_empty()
    {
        nint gone;
        using (var tree = HiddenWindows.Create())
        {
            gone = tree.Add(0, "FolderSwitch test root");
        }

        var info = WindowInspector.Snapshot(gone);

        Assert.Equal("", info.ClassName);
        Assert.Empty(info.DescendantClassNames);
    }

    /// <summary>Hidden <c>STATIC</c> windows created on the calling thread, destroyed on dispose (same thread).</summary>
    private sealed class HiddenWindows : IDisposable
    {
        private const uint WsChild = 0x40000000;
        private const uint WsPopup = 0x80000000;

        private readonly List<nint> _roots = [];

        public static HiddenWindows Create() => new();

        /// <summary>A child of <paramref name="parent"/> (a top-level window when zero).</summary>
        public nint Add(nint parent, string? title = null, int id = 0)
        {
            var window = CreateWindowEx(0, "STATIC", title, parent == 0 ? WsPopup : WsChild, 0, 0, 10, 10, parent, id, 0, 0);
            Assert.NotEqual(0, window);
            if (parent == 0)
            {
                _roots.Add(window);
            }

            return window;
        }

        public void Dispose()
        {
            // DestroyWindow destroys the children too.
            foreach (var root in _roots)
            {
                DestroyWindow(root);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
        private static extern nint CreateWindowEx(
            uint exStyle, string className, string? windowName, uint style, int x, int y, int width, int height,
            nint parent, nint menuOrId, nint instance, nint param);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(nint hwnd);
    }
}
