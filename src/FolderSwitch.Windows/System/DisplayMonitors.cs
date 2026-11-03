// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using FolderSwitch.Core.Layout;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FolderSwitch.Windows.SystemServices;

/// <summary>
/// The work area of a monitor (the screen minus the taskbar), in physical pixels, with the monitor's DPI
/// scale (1 at 96 DPI, 1.5 at 144 DPI...).
/// </summary>
public readonly record struct MonitorWorkArea(int Left, int Top, int Width, int Height, double Scale)
{
    /// <summary>
    /// The work area in device-independent pixels of that monitor: its pixels divided by its scale. A window placed
    /// in these coordinates goes back to pixels by multiplying by the same scale.
    /// </summary>
    public RectD ToDips() => new(Left / Scale, Top / Scale, Width / Scale, Height / Scale);
}

/// <summary>
/// A point in physical pixels (the mouse cursor, a window's top-left corner), with the work area of the monitor it
/// belongs to.
/// </summary>
public readonly record struct MonitorPoint(int X, int Y, MonitorWorkArea WorkArea)
{
    /// <summary>The point in device-independent pixels of its monitor, as <see cref="MonitorWorkArea.ToDips"/>.</summary>
    public (double X, double Y) ToDips() => (X / WorkArea.Scale, Y / WorkArea.Scale);
}

/// <summary>
/// The monitor under the mouse cursor (or the primary one, or the one holding a window), and moving a window in
/// physical pixels: what is needed to place a window on the screen the user is looking at, whatever its DPI.
/// </summary>
/// <remarks>Plain Win32 calls that never wait on another window; any thread. Nothing is thrown.</remarks>
public static class DisplayMonitors
{
    private const double BaseDpi = 96;

    /// <summary>
    /// The mouse cursor's position, with the work area of the monitor that holds it (the nearest one); <c>null</c> if
    /// either cannot be read.
    /// </summary>
    public static MonitorPoint? Cursor()
    {
        try
        {
            return PInvoke.GetCursorPos(out var cursor)
                && WorkArea(PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST)) is { } area
                ? new MonitorPoint(cursor.X, cursor.Y, area)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The top-left corner of a top-level window, with the work area of the monitor that holds the largest part of it
    /// (the nearest one when it is on none); <c>null</c> if either cannot be read.
    /// </summary>
    public static MonitorPoint? OfWindow(nint hwnd)
    {
        try
        {
            return hwnd != 0
                && PInvoke.GetWindowRect((HWND)hwnd, out var rect)
                && WorkArea(PInvoke.MonitorFromWindow((HWND)hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST)) is { } area
                ? new MonitorPoint(rect.left, rect.top, area)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The work area of the primary monitor (the one at the origin); <c>null</c> if it cannot be read.</summary>
    public static MonitorWorkArea? Primary()
    {
        try
        {
            return WorkArea(PInvoke.MonitorFromPoint(default, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Moves a top-level window so that its top-left corner is at (<paramref name="x"/>, <paramref name="y"/>) in
    /// physical pixels, without resizing, activating it or changing its z-order. Moved onto a monitor with another DPI,
    /// a per-monitor DPI aware window is rescaled by Windows (<c>WM_DPICHANGED</c>). Returns whether it moved.
    /// </summary>
    public static bool MoveWindow(nint hwnd, int x, int y)
    {
        try
        {
            return hwnd != 0 && PInvoke.SetWindowPos(
                (HWND)hwnd,
                HWND.Null,
                x,
                y,
                0,
                0,
                SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static MonitorWorkArea? WorkArea(HMONITOR monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (monitor.IsNull || !PInvoke.GetMonitorInfo(monitor, ref info))
        {
            return null;
        }

        // GetDpiForMonitor exists from Windows 8.1 on (the version the analyzer knows it by): always true on
        // the Windows 10 and 11 FolderSwitch runs on.
        var scale = 1.0;
        if (OperatingSystem.IsWindowsVersionAtLeast(8, 1)
            && PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded && dpiX > 0)
        {
            scale = dpiX / BaseDpi;
        }

        var work = info.rcWork;
        return new MonitorWorkArea(work.left, work.top, work.right - work.left, work.bottom - work.top, scale);
    }
}
