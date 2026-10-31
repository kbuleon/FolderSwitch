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
