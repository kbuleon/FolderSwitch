namespace FolderSwitch.Core.Layout;

/// <summary>
/// Column width rules, in device-independent pixels: 150 to 520 in steps of 10.
/// A column either has a fixed width (stored in the configuration) or is automatic and follows its content.
/// </summary>
public static class ColumnWidths
{
    public const int Min = 150;
    public const int Max = 520;
    public const int Step = 10;

    /// <summary>
    /// Automatic width for a measured content width: rounded <b>up</b> to the next multiple of
    /// <see cref="Step"/>, then clamped to <see cref="Min"/>..<see cref="Max"/>.
    /// A negative or NaN measure gives <see cref="Min"/>.
    /// </summary>
    public static int Auto(double measuredContentWidth)
    {
        if (double.IsNaN(measuredContentWidth))
        {
            return Min;
        }

        var rounded = Math.Ceiling(measuredContentWidth / Step) * Step;
        return (int)Math.Clamp(rounded, Min, Max);
    }

    /// <summary>
    /// Width actually used: a fixed width is kept (clamped to <see cref="Min"/>..<see cref="Max"/> defensively,
    /// although it is already stored clamped); without one, <see cref="Auto"/>.
    /// </summary>
    public static int Effective(int? fixedWidth, double measuredContentWidth) =>
        fixedWidth is { } width ? Math.Clamp(width, Min, Max) : Auto(measuredContentWidth);
}
