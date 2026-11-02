// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// Holds the board of every tab in one place, the board of <see cref="ActiveItem"/> on top: as wide as the widest
/// board and as tall as the active one. The window, sized to its content, thus keeps its width from one tab to another
/// (the tabs do not move under the mouse), and only its height follows the tab shown.
/// </summary>
/// <remarks>
/// Every child is measured, shown or not, for its width: the boards of the other tabs are hidden by their container
/// (<see cref="Visibility.Hidden"/> keeps them in the layout) rather than removed. A child is the active one when its
/// data context is <see cref="ActiveItem"/>.
/// </remarks>
public sealed class TabBoardsPanel : Panel
{
    public static readonly DependencyProperty ActiveItemProperty = DependencyProperty.Register(
        nameof(ActiveItem),
        typeof(object),
        typeof(TabBoardsPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The tab shown.</summary>
    public object? ActiveItem
    {
        get => GetValue(ActiveItemProperty);
        set => SetValue(ActiveItemProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(availableSize);
            width = Math.Max(width, child.DesiredSize.Width);
            if (IsActive(child))
            {
                height = child.DesiredSize.Height;
            }
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            var height = IsActive(child) ? finalSize.Height : child.DesiredSize.Height;
            child.Arrange(new Rect(0, 0, finalSize.Width, height));
        }

        return finalSize;
    }

    private bool IsActive(UIElement child) =>
        child is FrameworkElement { DataContext: { } item } && ReferenceEquals(item, ActiveItem);
}
