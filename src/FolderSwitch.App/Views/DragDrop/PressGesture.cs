// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Input;

namespace FolderSwitch.App.Views.DragDrop;

/// <summary>
/// Turns the left button's presses on an element into a click, a double-click or the start of a drag, as a button
/// would but without its capture fighting a drag: the press captures the mouse, moving past the system's drag
/// threshold releases it and starts the drag instead of the click, and a release over the element is the click.
/// One per element (<see cref="For"/>); what each gesture does is the view's (the callbacks).
/// </summary>
/// <remarks>
/// <para>
/// The element handles the press, so that the elements around it (a group header around its grip) do not see it.
/// A button inside it (✕, the color dot) handles its own press first and is never seen here.
/// </para>
/// <para>UI thread only.</para>
/// </remarks>
internal sealed class PressGesture
{
    private static readonly DependencyProperty GestureProperty = DependencyProperty.RegisterAttached(
        "Gesture",
        typeof(PressGesture),
        typeof(PressGesture));

    private readonly UIElement _element;
    private Point _start;
    private bool _pressed;
    private int _clickCount;

    private PressGesture(UIElement element)
    {
        _element = element;
        element.MouseLeftButtonDown += OnDown;
        element.MouseMove += OnMove;
        element.MouseLeftButtonUp += OnUp;
        element.LostMouseCapture += OnLostCapture;
    }

    /// <summary>A press released over the element; the argument is the press's click count (2 for the second of a double-click).</summary>
    public Action<int>? Click { get; set; }

    /// <summary>
    /// The second press of a double-click: returns true when it did something (then no click follows), false to let it
    /// be an ordinary press.
    /// </summary>
    public Func<bool>? DoubleClick { get; set; }

    /// <summary>Whether a press may become a drag now (Customize mode only, say).</summary>
    public Func<bool>? CanDrag { get; set; }

    /// <summary>The mouse moved past the drag threshold with the button down: starts the drag (and blocks until it ends).</summary>
    public Action? Drag { get; set; }

    /// <summary>The element the current or last press landed on (the press's original source).</summary>
    public object? PressedSource { get; private set; }

    /// <summary>The gesture of <paramref name="element"/>, created on first use.</summary>
    public static PressGesture For(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (element.GetValue(GestureProperty) is not PressGesture gesture)
        {
            gesture = new PressGesture(element);
            element.SetValue(GestureProperty, gesture);
        }

        return gesture;
    }

    /// <summary>True when the press landed on <paramref name="part"/> or inside it.</summary>
    public bool PressedOn(DependencyObject? part) =>
        part is not null && TreeHelpers.IsWithin(PressedSource as DependencyObject, part);

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        PressedSource = e.OriginalSource;
        e.Handled = true;
        if (e.ClickCount >= 2 && DoubleClick?.Invoke() == true)
        {
            _pressed = false;
            return;
        }

        _start = e.GetPosition(_element);
        _clickCount = e.ClickCount;
        _pressed = _element.CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed || Drag is null || CanDrag?.Invoke() != true)
        {
            return;
        }

        var position = e.GetPosition(_element);
        if (Math.Abs(position.X - _start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        // A drag, not a click: the capture goes, so that the release ends the drag and not a click.
        _pressed = false;
        _element.ReleaseMouseCapture();
        Drag();
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        e.Handled = true;
        var position = e.GetPosition(_element);
        var over = position.X >= 0 && position.Y >= 0
            && position.X <= _element.RenderSize.Width && position.Y <= _element.RenderSize.Height;
        _element.ReleaseMouseCapture();
        if (over)
        {
            Click?.Invoke(_clickCount);
        }
    }

    private void OnLostCapture(object sender, MouseEventArgs e) => _pressed = false;
}
