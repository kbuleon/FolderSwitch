// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;

namespace FolderSwitch.App.Views.Layout;

/// <summary>A stack of its visible children with <see cref="Spacing"/> DIPs between two of them.</summary>
public sealed class SpacingPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(Orientation),
        typeof(SpacingPanel),
        new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(SpacingPanel),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var childConstraint = horizontal
            ? new Size(double.PositiveInfinity, availableSize.Height)
            : new Size(availableSize.Width, double.PositiveInfinity);

        double along = 0;
        double across = 0;
        var visible = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(childConstraint);
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var size = child.DesiredSize;
            along += horizontal ? size.Width : size.Height;
            across = Math.Max(across, horizontal ? size.Height : size.Width);
            visible++;
        }

        along += Math.Max(0, visible - 1) * Spacing;
        return horizontal ? new Size(along, across) : new Size(across, along);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        double offset = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var size = child.DesiredSize;
            if (horizontal)
            {
                child.Arrange(new Rect(offset, 0, size.Width, finalSize.Height));
                offset += size.Width + Spacing;
            }
            else
            {
                child.Arrange(new Rect(0, offset, finalSize.Width, size.Height));
                offset += size.Height + Spacing;
            }
        }

        return finalSize;
    }
}
