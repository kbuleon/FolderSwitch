// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// Clips its content to a rectangle with rounded corners of its own size, as CSS <c>overflow: hidden</c> does inside
/// a box with <c>border-radius</c>: a group's header fill and colored edges follow the group's corners.
/// </summary>
public sealed class RoundedClip : Decorator
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(RoundedClip),
        new FrameworkPropertyMetadata(default(CornerRadius), FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>The radius of the corners; only <see cref="System.Windows.CornerRadius.TopLeft"/> is used (all four alike).</summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var size = base.ArrangeOverride(arrangeSize);
        var radius = Math.Max(0, CornerRadius.TopLeft);
        var clip = new RectangleGeometry(new Rect(size), radius, radius);
        clip.Freeze();
        Clip = clip;
        return size;
    }
}
