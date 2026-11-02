// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// The row of an item: its icon, its name, then its secondary text (the path), side by side and centered vertically.
/// </summary>
/// <remarks>
/// <para>
/// The name comes first: it gets all the width it needs, then the secondary text gets what is left, and is hidden
/// when that is less than <see cref="MinSecondaryWidth"/> (a lone ellipsis says nothing; the path is in the tooltip).
/// When the row is short of room for the name too, the name is given what there is and ellipsized by its
/// <c>TextBlock</c>.
/// </para>
/// <para>
/// Measured without a width limit, the row asks for its icon and name only: the secondary text never widens an
/// automatic column (spec 5.2: an automatic column fits the longest group or item name).
/// </para>
/// <para>Children, in order: icon, name, secondary text; any further child is ignored.</para>
/// </remarks>
public sealed class ItemRowPanel : Panel
{
    /// <summary>Below this width the secondary text is not shown at all.</summary>
    public const double MinSecondaryWidth = 44;

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(ItemRowPanel),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    private double _naturalNameWidth;
    private double _nameWidth;
    private double _secondaryWidth;

    /// <summary>Space between the icon and the name, and between the name and the secondary text.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    private UIElement? Icon => InternalChildren.Count > 0 ? InternalChildren[0] : null;

    private UIElement? Label => InternalChildren.Count > 1 ? InternalChildren[1] : null;

    private UIElement? Secondary => InternalChildren.Count > 2 ? InternalChildren[2] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        Icon?.Measure(Unbounded);
        Label?.Measure(Unbounded);

        var iconWidth = Icon?.DesiredSize.Width ?? 0;
        // One device pixel more than the name: its desired width may have been rounded under its own (see TextRoom).
        var naturalName = Label is null ? 0 : Label.DesiredSize.Width + TextRoom.DevicePixel(this);
        _naturalNameWidth = naturalName;
        var height = Math.Max(Icon?.DesiredSize.Height ?? 0, Label?.DesiredSize.Height ?? 0);

        if (double.IsPositiveInfinity(availableSize.Width))
        {
            _nameWidth = naturalName;
            _secondaryWidth = 0;
            Secondary?.Measure(new Size(0, double.PositiveInfinity));
            return new Size(iconWidth + Spacing + naturalName, height);
        }

        Split(availableSize.Width, iconWidth, naturalName);
        Label?.Measure(new Size(_nameWidth, double.PositiveInfinity));
        Secondary?.Measure(new Size(_secondaryWidth, double.PositiveInfinity));
        if (_secondaryWidth > 0)
        {
            height = Math.Max(height, Secondary!.DesiredSize.Height);
        }

        var used = iconWidth + Spacing + _nameWidth + (_secondaryWidth > 0 ? Spacing + _secondaryWidth : 0);
        return new Size(Math.Min(availableSize.Width, used), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var iconWidth = Icon?.DesiredSize.Width ?? 0;
        Split(finalSize.Width, iconWidth, _naturalNameWidth);

        var x = 0.0;
        if (Icon is { } icon)
        {
            icon.Arrange(Centered(x, iconWidth, icon.DesiredSize.Height, finalSize.Height));
            x += iconWidth + Spacing;
        }

        if (Label is { } name)
        {
            name.Arrange(Centered(x, _nameWidth, name.DesiredSize.Height, finalSize.Height));
            x += _nameWidth + Spacing;
        }

        if (Secondary is { } secondary)
        {
            // Hidden by an empty slot rather than by its visibility: changing that from a layout pass would start another.
            secondary.Arrange(_secondaryWidth > 0
                ? Centered(x, _secondaryWidth, secondary.DesiredSize.Height, finalSize.Height)
                : new Rect(x, 0, 0, 0));
        }

        return finalSize;
    }

    private static Rect Centered(double x, double width, double height, double rowHeight) =>
        new(x, Math.Max(0, (rowHeight - height) / 2), width, Math.Min(height, rowHeight));

    /// <summary>Shares <paramref name="width"/>: the name first, the secondary text what is left (or nothing).</summary>
    private void Split(double width, double iconWidth, double naturalName)
    {
        var rest = Math.Max(0, width - iconWidth - Spacing);
        _nameWidth = Math.Min(naturalName, rest);
        var left = rest - _nameWidth - Spacing;
        _secondaryWidth = Secondary is not null && left >= MinSecondaryWidth ? left : 0;
    }
}
