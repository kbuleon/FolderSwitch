// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// Shows its content, or while <see cref="IsCollapsed"/> takes no height at all and shows nothing of it (the mockups'
/// groups collapse the same way).
/// </summary>
public sealed class CollapsibleBox : Decorator
{
    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(
        nameof(IsCollapsed),
        typeof(bool),
        typeof(CollapsibleBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public CollapsibleBox()
    {
        ClipToBounds = true;
    }

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is not { } child)
        {
            return default;
        }

        child.Measure(constraint);
        return IsCollapsed ? new Size(child.DesiredSize.Width, 0) : child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (Child is { } child)
        {
            // Collapsed: laid out at its own height, but clipped to none of it (and so neither drawn nor hit).
            child.Arrange(new Rect(0, 0, arrangeSize.Width, IsCollapsed ? child.DesiredSize.Height : arrangeSize.Height));
        }

        return arrangeSize;
    }
}
