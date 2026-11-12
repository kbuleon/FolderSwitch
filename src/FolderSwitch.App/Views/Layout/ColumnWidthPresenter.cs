// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using FolderSwitch.Core.Layout;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// Gives its child (the groups of a column) the column's width: <see cref="PreviewWidth"/> while the column is being
/// resized, else <see cref="FixedWidth"/>, bounded by <see cref="ColumnWidths"/>. The content never widens the column:
/// every group of the column is measured and arranged at that width, and a name too long for it is ellipsized.
/// </summary>
public sealed class ColumnWidthPresenter : Decorator
{
    public static readonly DependencyProperty FixedWidthProperty = DependencyProperty.Register(
        nameof(FixedWidth),
        typeof(int),
        typeof(ColumnWidthPresenter),
        new FrameworkPropertyMetadata(ColumnWidths.Default, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty PreviewWidthProperty = DependencyProperty.Register(
        nameof(PreviewWidth),
        typeof(int?),
        typeof(ColumnWidthPresenter),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    private double _width = ColumnWidths.Default;

    /// <summary>The column's width, in DIPs.</summary>
    public int FixedWidth
    {
        get => (int)GetValue(FixedWidthProperty);
        set => SetValue(FixedWidthProperty, value);
    }

    /// <summary>
    /// While the column's resize grip is dragged: the width under the mouse, drawn in place of the column's own until
    /// the gesture ends (<c>null</c> otherwise). Not recorded: the grip records the width once, at the end.
    /// </summary>
    public int? PreviewWidth
    {
        get => (int?)GetValue(PreviewWidthProperty);
        set => SetValue(PreviewWidthProperty, value);
    }

    /// <summary>The width the column has after the last measure.</summary>
    public double EffectiveWidth => _width;

    protected override Size MeasureOverride(Size constraint)
    {
        _width = Math.Clamp(PreviewWidth ?? FixedWidth, ColumnWidths.Min, ColumnWidths.Max);
        if (Child is not { } child)
        {
            return new Size(_width, 0);
        }

        child.Measure(new Size(_width, constraint.Height));
        return new Size(_width, child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Child?.Arrange(new Rect(0, 0, _width, arrangeSize.Height));
        return new Size(_width, arrangeSize.Height);
    }
}
