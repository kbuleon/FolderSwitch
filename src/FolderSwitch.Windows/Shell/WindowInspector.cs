// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace FolderSwitch.Windows.Shell;

/// <summary>
/// Reads what <see cref="WindowClassifier"/> needs about a window, and finds windows in its tree. Plain
/// Win32 calls that never send a message to the window, so a hung application cannot block them; any
/// thread.
/// </summary>
internal static class WindowInspector
{
    /// <summary>Descendants deeper than this (children are at depth 1) are not visited.</summary>
    public const int MaxDepth = 6;

    /// <summary>At most this many descendants are visited.</summary>
    public const int MaxWindows = 300;

    /// <summary>
    /// The class name, process name (without <c>.exe</c>) and descendants of <paramref name="hwnd"/>.
    /// Descendants are listed breadth first, so the shallow ones, which identify the dialogs, come
    /// first when the <see cref="MaxWindows"/> limit is reached. Empty values for a window that is gone.
    /// </summary>
    public static WindowInfo Snapshot(nint hwnd)
    {
        var classes = new List<string>();
        var ids = new List<int>();
        var className = ClassName(hwnd);
        if (className.Length == 0)
        {
            return new WindowInfo("", "", classes) { DescendantControlIds = ids };
        }

        foreach (var (child, _) in Descendants(hwnd))
        {
            classes.Add(ClassName(child));
            ids.Add(PInvoke.GetDlgCtrlID((HWND)child));
        }

        return new WindowInfo(className, ProcessName(hwnd), classes) { DescendantControlIds = ids };
    }

    /// <summary>
    /// The first descendant of <paramref name="root"/> (breadth first, within <see cref="MaxDepth"/>
    /// and <see cref="MaxWindows"/>) of that class, or zero.
    /// </summary>
    public static nint FindDescendant(nint root, string className)
    {
        foreach (var (child, _) in Descendants(root))
        {
            if (string.Equals(ClassName(child), className, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return 0;
    }

    /// <summary>The first direct child of <paramref name="parent"/> of that class, or zero.</summary>
    public static nint FindChild(nint parent, string className) =>
        parent == 0 ? 0 : (nint)PInvoke.FindWindowEx((HWND)parent, HWND.Null, className, null);

    /// <summary>The Win32 class name, or an empty string for a window that is gone.</summary>
    public static unsafe string ClassName(nint hwnd)
    {
        if (hwnd == 0)
        {
            return "";
        }

        // Class names are at most 256 characters.
        Span<char> buffer = stackalloc char[257];
        var length = PInvoke.GetClassName((HWND)hwnd, buffer);
        return length > 0 ? new string(buffer[..length]) : "";
    }

    /// <summary>The name of the process owning the window, without <c>.exe</c>; empty if it cannot be read.</summary>
    public static string ProcessName(nint hwnd)
    {
        if (PInvoke.GetWindowThreadProcessId((HWND)hwnd, out var processId) == 0)
        {
            return "";
        }

        // PROCESS_QUERY_LIMITED_INFORMATION is granted even for most elevated processes.
        using var process = PInvoke.OpenProcess_SafeHandle(
            PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            processId);
        if (process.IsInvalid)
        {
            return "";
        }

        Span<char> buffer = stackalloc char[1024];
        var size = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref size)
            ? Path.GetFileNameWithoutExtension(new string(buffer[..(int)size]))
            : "";
    }

    /// <summary>
    /// The descendants of <paramref name="root"/>, breadth first, with their depth (children at 1),
    /// within <see cref="MaxDepth"/> and <see cref="MaxWindows"/>. <c>FindWindowEx</c> lists the
    /// direct children of a window, which keeps track of the depth (<c>EnumChildWindows</c> flattens
    /// the whole tree).
    /// </summary>
    private static IEnumerable<(nint Window, int Depth)> Descendants(nint root)
    {
        if (root == 0)
        {
            yield break;
        }

        var visited = 0;
        var pending = new Queue<(nint Window, int Depth)>();
        pending.Enqueue((root, 0));
        while (pending.Count > 0)
        {
            var (parent, depth) = pending.Dequeue();
            if (depth == MaxDepth)
            {
                continue;
            }

            var child = HWND.Null;
            while (true)
            {
                child = PInvoke.FindWindowEx((HWND)parent, child, null, null);
                if (child.IsNull)
                {
                    break;
                }

                yield return ((nint)child, depth + 1);
                if (++visited == MaxWindows)
                {
                    yield break;
                }

                pending.Enqueue(((nint)child, depth + 1));
            }
        }
    }
}
