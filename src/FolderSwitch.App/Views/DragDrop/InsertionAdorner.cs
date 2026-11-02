// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FolderSwitch.App.Themes;

namespace FolderSwitch.App.Views.DragDrop;

/// <summary>
/// The insertion indicator of a drag and drop: a line in the accent color, with a dot at each end, across the list
/// it is drawn over (horizontal between two groups or items, vertical between two tabs), at <see cref="Position"/>.
/// </summary>
internal sealed class InsertionAdorner : Adorner
{
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(InsertionAdorner),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double Thickness = 2.5;
    private const double DotRadius = 3.25;

    private readonly Orientation _orientation;
    private double _position;
    private double _from;
    private double _to;

    /// <param name="orientation">How the list runs: vertical (a line across, between rows) or horizontal (a line down, between tabs).</param>
    public InsertionAdorner(UIElement list, Orientation orientation)
        : base(list)
    {
        _orientation = orientation;
        IsHitTestVisible = false;
        SetResourceReference(LineBrushProperty, ThemeKeys.Accent);
    }

    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>Where the line runs across the list, in its coordinates (y for a vertical list, x for a horizontal one).</summary>
    public double Position => _position;

    /// <summary>Moves the line: <paramref name="position"/> along the list, from <paramref name="from"/> to <paramref name="to"/> across it.</summary>
    public void Place(double position, double from, double to)
    {
        if (position == _position && from == _from && to == _to)
        {
            return;
        }

        _position = position;
        _from = from;
        _to = to;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (LineBrush is not { } brush || _to <= _from)
        {
            return;
        }

        var vertical = _orientation == Orientation.Vertical;
        var start = vertical ? new Point(_from, _position) : new Point(_position, _from);
        var end = vertical ? new Point(_to, _position) : new Point(_position, _to);
        var line = vertical
            ? new Rect(start.X, start.Y - (Thickness / 2), end.X - start.X, Thickness)
            : new Rect(start.X - (Thickness / 2), start.Y, Thickness, end.Y - start.Y);

        drawingContext.DrawRoundedRectangle(brush, null, line, Thickness / 2, Thickness / 2);
        drawingContext.DrawEllipse(brush, null, start, DotRadius, DotRadius);
        drawingContext.DrawEllipse(brush, null, end, DotRadius, DotRadius);
    }
}
