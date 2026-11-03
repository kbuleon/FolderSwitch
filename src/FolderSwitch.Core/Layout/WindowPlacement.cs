// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Layout;

/// <summary>
/// Window placement rules, in device-independent pixels. The work area is the screen minus the taskbar;
/// its origin may be negative (monitors left of or above the primary one).
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// Centers a window of <paramref name="size"/> in <paramref name="workArea"/>. On an axis where the size
    /// exceeds the work area, the size is clamped to it and the window is aligned to the work area's origin.
    /// </summary>
    public static RectD CenterOn(SizeD size, RectD workArea)
    {
        var clamped = ClampToWorkArea(size, workArea);
        return new RectD(
            workArea.X + (workArea.Width - clamped.Width) / 2,
            workArea.Y + (workArea.Height - clamped.Height) / 2,
            clamped.Width,
            clamped.Height);
    }

    /// <summary>
    /// Places a window of <paramref name="newSize"/> keeping the <b>top edge</b> and the <b>horizontal center</b>
    /// of <paramref name="current"/>, then brings it fully inside <paramref name="workArea"/>
    /// (size clamped first; the top edge is kept unless the bottom overflows, in which case the window moves up).
    /// </summary>
    public static RectD Reanchor(RectD current, SizeD newSize, RectD workArea)
    {
        var size = ClampToWorkArea(newSize, workArea);
        var centerX = current.X + current.Width / 2;

        return new RectD(
            Fit(centerX - size.Width / 2, size.Width, workArea.X, workArea.Width),
            Fit(current.Y, size.Height, workArea.Y, workArea.Height),
            size.Width,
            size.Height);
    }

    /// <summary>
    /// Centers a window of <paramref name="size"/> on the point (<paramref name="x"/>, <paramref name="y"/>),
    /// then shifts it the least possible on each axis so it lies fully inside <paramref name="workArea"/>
    /// (size clamped first).
    /// </summary>
    public static RectD CenterOnPoint(SizeD size, double x, double y, RectD workArea)
    {
        var clamped = ClampToWorkArea(size, workArea);
        return new RectD(
            Fit(x - clamped.Width / 2, clamped.Width, workArea.X, workArea.Width),
            Fit(y - clamped.Height / 2, clamped.Height, workArea.Y, workArea.Height),
            clamped.Width,
            clamped.Height);
    }

    /// <summary>
    /// Brings <paramref name="rect"/> fully inside <paramref name="workArea"/>: the size is clamped first,
    /// then the position is shifted the least possible on each axis.
    /// </summary>
    public static RectD FitInside(RectD rect, RectD workArea)
    {
        var size = ClampToWorkArea(rect.Size, workArea);
        return new RectD(
            Fit(rect.X, size.Width, workArea.X, workArea.Width),
            Fit(rect.Y, size.Height, workArea.Y, workArea.Height),
            size.Width,
            size.Height);
    }

    /// <summary>Clamps the width and the height independently to the work area's.</summary>
    public static SizeD ClampToWorkArea(SizeD desired, RectD workArea) =>
        new(Math.Min(desired.Width, workArea.Width), Math.Min(desired.Height, workArea.Height));

    /// <summary>
    /// Shifts <paramref name="start"/> the least possible so that <c>[start, start + length]</c> lies inside
    /// <c>[areaStart, areaStart + areaLength]</c>. <paramref name="length"/> must not exceed <paramref name="areaLength"/>.
    /// </summary>
    private static double Fit(double start, double length, double areaStart, double areaLength)
    {
        // Math.Max guards the bound against floating-point rounding: (areaStart + areaLength) - length
        // can land one ulp under areaStart when length == areaLength, and Math.Clamp throws when min > max.
        var latestStart = Math.Max(areaStart, areaStart + areaLength - length);
        return Math.Clamp(start, areaStart, latestStart);
    }
}
