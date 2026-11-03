// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;

namespace FolderSwitch.App.Views.Layout;

/// <summary>
/// The left of the title bar: its children side by side, the last one shown only when it fits
/// whole in the width the row is given.
/// </summary>
/// <remarks>
/// <para>
/// The row never asks for the last child's width: the window, sized to its content, is as wide as the board and the
/// title bar's other children make it, and the last child (the Ko-fi button) never widens it. When the row is given
/// less than all its children need, the last one is arranged in an empty rectangle, so it is neither drawn nor
/// clicked, and the others keep their place.
/// </para>
/// <para>Children's margins space them, their vertical alignment places them; a collapsed child takes no room.</para>
/// </remarks>
public sealed class TitleRowPanel : Panel
{
    private static readonly Size Unbounded = new(double.PositiveInfinity, double.PositiveInfinity);

    /// <summary>True when the last child was given room at the last arrange.</summary>
    internal bool IsLastShown { get; private set; }

    private UIElement? Last => InternalChildren.Count > 0 ? InternalChildren[^1] : null;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(Unbounded);
            height = Math.Max(height, child.DesiredSize.Height);
            if (!ReferenceEquals(child, Last))
            {
                width += child.DesiredSize.Width;
            }
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double offset = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (ReferenceEquals(child, Last))
            {
                break;
            }

            ArrangeAt(child, offset, finalSize.Height);
            offset += child.DesiredSize.Width;
        }

        if (Last is { } last)
        {
            IsLastShown = last.Visibility != Visibility.Collapsed && offset + last.DesiredSize.Width <= finalSize.Width;
            if (IsLastShown)
            {
                ArrangeAt(last, offset, finalSize.Height);
            }
            else
            {
                last.Arrange(new Rect());
            }
        }

        return finalSize;
    }

    private static void ArrangeAt(UIElement child, double offset, double height) =>
        child.Arrange(new Rect(offset, 0, child.DesiredSize.Width, height));
}
