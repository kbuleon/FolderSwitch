// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using FolderSwitch.App.Themes;

namespace FolderSwitch.App.Views;

/// <summary>Which edge of its anchor a flyout lines up with.</summary>
internal enum FlyoutAlignment
{
    /// <summary>Its left edge under the anchor's left edge (« Add ▾ », the icon picker).</summary>
    Left,

    /// <summary>Its right edge under the anchor's right edge (the palette).</summary>
    Right,
}

/// <summary>
/// The menus, one at a time: in Customize mode the « Add ▾ » menu and the palette, the Edit menu, the icon picker; an item's menu. Each is shown below its anchor
/// in a frame of the theme (<see cref="ThemeKeys.Menu"/>, rounded, with a shadow), in a popup that closes on a click outside it; the window also closes it on Escape, when it
/// loses the focus or hides, and when Customize mode ends (see <see cref="MainView"/>).
/// </summary>
/// <remarks>
/// <para>
/// The popup is declared in <see cref="MainView"/>, so that it is part of its logical tree: a change of appearance
/// reaches what it shows as it reaches the window (a popup outside the tree would keep the old colors).
/// </para>
/// <para>
/// A click on the anchor of an open flyout closes it; that click must not open it again, so a flyout is not reopened
/// from the anchor it just closed from, for a moment.
/// </para>
/// <para>
/// The snapshots have no window and no popup: with <see cref="InlineLayer"/> set, the frame is put in that layer, where
/// the popup would be, and drawn with everything else.
/// </para>
/// </remarks>
internal sealed class Flyout
{
    /// <summary>Room around the frame for its shadow (the popup is transparent there).</summary>
    private const double ShadowRoom = 16;

    /// <summary>Space between the anchor and the frame.</summary>
    private const double Gap = 4;

    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly Popup _popup;
    private readonly Border _host;
    private readonly Border _frame;
    private FrameworkElement? _anchor;
    private FrameworkElement? _closedAnchor;
    private long _closedAt;
    private bool _inline;

    public Flyout(Popup popup)
    {
        ArgumentNullException.ThrowIfNull(popup);

        _popup = popup;
        _popup.StaysOpen = false;
        _popup.AllowsTransparency = true;
        _popup.Focusable = false;
        _popup.PopupAnimation = PopupAnimation.None;
        _popup.Placement = PlacementMode.Bottom;

        _frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(7),
            SnapsToDevicePixels = true,
        };
        _frame.SetResourceReference(Border.BackgroundProperty, ThemeKeys.Menu);
        _frame.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Border);
        _frame.SetResourceReference(UIElement.EffectProperty, ThemeKeys.FlyoutShadow);
        _frame.SetResourceReference(TextElement.ForegroundProperty, ThemeKeys.MenuText);
        TextElement.SetFontFamily(_frame, new FontFamily("Segoe UI"));
        TextElement.SetFontSize(_frame, 13);

        _host = new Border { Padding = new Thickness(ShadowRoom), Background = Brushes.Transparent, Child = _frame };
        _popup.Closed += OnPopupClosed;
    }

    /// <summary>Raised once a flyout closed, with what it showed.</summary>
    public event EventHandler<FrameworkElement>? Closed;

    /// <summary>What the open flyout shows; <c>null</c> when none is open.</summary>
    public FrameworkElement? Content { get; private set; }

    /// <summary>The snapshots: the layer the frame is put in instead of the popup.</summary>
    public Canvas? InlineLayer { get; set; }

    /// <summary>
    /// Shows <paramref name="content"/> below <paramref name="anchor"/>, in place of any other flyout. A click on the
    /// anchor of the open flyout closes it instead. Returns whether the flyout opened.
    /// </summary>
    public bool Open(FrameworkElement content, FrameworkElement anchor, FlyoutAlignment alignment)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(anchor);

        if (Content is not null && ReferenceEquals(anchor, _anchor))
        {
            Close();
            return false;
        }

        if (ReferenceEquals(anchor, _closedAnchor) && Stopwatch.GetElapsedTime(_closedAt) < ReopenGuard)
        {
            return false;
        }

        Close();
        Content = content;
        _anchor = anchor;
        _frame.Child = content;

        var x = alignment == FlyoutAlignment.Left ? 0 : anchor.ActualWidth - FrameWidth(content);
        if (InlineLayer is { } layer)
        {
            _inline = true;
            var origin = anchor.TransformToVisual(layer).Transform(new Point(x - ShadowRoom, anchor.ActualHeight + Gap - ShadowRoom));
            Canvas.SetLeft(_host, origin.X);
            Canvas.SetTop(_host, origin.Y);
            layer.Children.Add(_host);
            return true;
        }

        _inline = false;
        _popup.Child = _host;
        _popup.PlacementTarget = anchor;
        _popup.HorizontalOffset = x - ShadowRoom;
        _popup.VerticalOffset = Gap - ShadowRoom;
        _popup.IsOpen = true;
        return true;
    }

    /// <summary>
    /// The window moved (it keeps its top edge and center as its content changes size): the popup follows its anchor.
    /// </summary>
    public void Reposition()
    {
        if (Content is null || _inline)
        {
            return;
        }

        // A popup places itself again when an offset changes.
        var offset = _popup.HorizontalOffset;
        _popup.HorizontalOffset = offset + 1;
        _popup.HorizontalOffset = offset;
    }

    /// <summary>Closes the open flyout; returns false when none was open.</summary>
    public bool Close()
    {
        if (Content is null)
        {
            return false;
        }

        if (_inline)
        {
            InlineLayer?.Children.Remove(_host);
            Finish();
        }
        else
        {
            // Closed follows, and finishes.
            _popup.IsOpen = false;
            if (Content is not null)
            {
                Finish();
            }
        }

        return true;
    }

    /// <summary>The frame's width: the content's own (every flyout sets one), its padding and its border.</summary>
    private double FrameWidth(FrameworkElement content)
    {
        var width = content.Width;
        if (double.IsNaN(width))
        {
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width = content.DesiredSize.Width;
        }

        return width + _frame.Padding.Left + _frame.Padding.Right + _frame.BorderThickness.Left + _frame.BorderThickness.Right;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _closedAnchor = _anchor;
        _closedAt = Stopwatch.GetTimestamp();
        Finish();
    }

    private void Finish()
    {
        var content = Content;
        Content = null;
        _anchor = null;
        _frame.Child = null;
        if (content is not null)
        {
            Closed?.Invoke(this, content);
        }
    }
}
