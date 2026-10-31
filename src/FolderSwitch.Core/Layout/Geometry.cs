namespace FolderSwitch.Core.Layout;

/// <summary>A width and a height, in device-independent pixels (WPF DIPs).</summary>
public readonly record struct SizeD(double Width, double Height);

/// <summary>
/// A rectangle in device-independent pixels (WPF DIPs). <see cref="X"/> and <see cref="Y"/> are the
/// top-left corner and may be negative (a monitor left of or above the primary one).
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    /// <summary>X of the right edge.</summary>
    public double Right => X + Width;

    /// <summary>Y of the bottom edge.</summary>
    public double Bottom => Y + Height;

    /// <summary>Width and height as a <see cref="SizeD"/>.</summary>
    public SizeD Size => new(Width, Height);
}
