// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FolderSwitch.App.Views.Layout;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The rename in place of a tab, a group or an item: a text box laid over the name, shown while its view model
/// <see cref="RenamableViewModel.IsRenaming"/> (set <see cref="IsActiveProperty"/> to that, and the box's data context
/// to the view model). When the rename starts, the box gets the name shown, all of it selected, and the keyboard focus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keys.</b> Enter commits (<see cref="MainViewModel.CommitRename"/>), Escape cancels
/// (<see cref="MainViewModel.CancelRename"/>) without closing the window, Tab, Shift+Tab and F2 stay in the box (they
/// neither change tab nor start the rename again). Losing the keyboard focus commits: another window took it, or a click elsewhere in the window
/// (<see cref="MainView"/> moves the focus back to the window on such a click, since nothing else in it takes the
/// focus). A blank name is refused by the view model, which keeps the old one.
/// </para>
/// <para>
/// <b>Focus.</b> When the rename ends from the keyboard, the focus goes back to the window, so that Tab, Escape and F2
/// keep working.
/// </para>
/// </remarks>
public static class InlineRename
{
    /// <summary>Bound to <see cref="RenamableViewModel.IsRenaming"/>: the rename runs while true.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
        "IsActive",
        typeof(bool),
        typeof(InlineRename),
        new FrameworkPropertyMetadata(false, OnIsActiveChanged));

    private static readonly DependencyProperty IsEndingProperty = DependencyProperty.RegisterAttached(
        "IsEnding",
        typeof(bool),
        typeof(InlineRename));

    private static readonly DependencyProperty IsWiredProperty = DependencyProperty.RegisterAttached(
        "IsWired",
        typeof(bool),
        typeof(InlineRename));

    public static bool GetIsActive(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsActiveProperty);
    }

    public static void SetIsActive(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsActiveProperty, value);
    }

    /// <summary>True for a text box of a rename in place.</summary>
    public static bool IsRenameBox(object? element) => element is TextBox box && box.GetValue(IsWiredProperty) is true;

    /// <summary>Ends the rename of <paramref name="box"/>, keeping what was typed: a click elsewhere in the window.</summary>
    public static void Commit(TextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        End(box, commit: true);
    }

    /// <summary>The name a rename starts from: the one shown.</summary>
    private static string NameOf(object? target) => target switch
    {
        TabViewModel tab => tab.Name,
        GroupViewModel group => group.Name,
        ItemViewModel item => item.DisplayName,
        _ => string.Empty,
    };

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        Wire(box);

        // The box and the name take turns in the same place: an automatic column fits whichever shows.
        ColumnWidthPresenter.InvalidateColumnOf(box);
        if (e.NewValue is not true)
        {
            return;
        }

        box.Text = NameOf(box.DataContext);
        box.SelectAll();

        // Once the box is laid out (a new tab or group gets its views after this): the focus, so that typing replaces the name.
        box.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!GetIsActive(box))
            {
                return;
            }

            if (box.IsVisible)
            {
                box.Focus();
            }

            box.SelectAll();
        });
    }

    private static void Wire(TextBox box)
    {
        if (box.GetValue(IsWiredProperty) is true)
        {
            return;
        }

        box.SetValue(IsWiredProperty, true);
        box.PreviewKeyDown += OnPreviewKeyDown;
        box.LostKeyboardFocus += OnLostKeyboardFocus;
        box.TextChanged += OnTextChanged;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                End(box, commit: true);
                break;
            case Key.Escape:
                e.Handled = true;
                End(box, commit: false);
                break;
            case Key.Tab:
            case Key.F2:
                // Neither another tab nor another control, nor a new rename over this one: the box keeps the keyboard.
                e.Handled = true;
                break;
        }
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => End((TextBox)sender, commit: true);

    /// <summary>The box sizes an automatic column like the name it covers: the column fits what is typed.</summary>
    private static void OnTextChanged(object sender, TextChangedEventArgs e) => ColumnWidthPresenter.InvalidateColumnOf((TextBox)sender);

    private static void End(TextBox box, bool commit)
    {
        if (box.GetValue(IsEndingProperty) is true || !GetIsActive(box))
        {
            return;
        }

        box.SetValue(IsEndingProperty, true);
        try
        {
            var hadFocus = box.IsKeyboardFocusWithin;
            if (MainView.Of(box)?.DataContext is MainViewModel viewModel && box.DataContext is { } target)
            {
                if (commit)
                {
                    viewModel.CommitRename(target, box.Text);
                }
                else
                {
                    viewModel.CancelRename(target);
                }
            }

            if (Window.GetWindow(box) is { } window)
            {
                if (hadFocus)
                {
                    window.Focus();
                }
                else if (ReferenceEquals(FocusManager.GetFocusedElement(window), box))
                {
                    // Ended while another window had the focus: the window must not hand it back to a hidden box.
                    FocusManager.SetFocusedElement(window, window);
                }
            }
        }
        finally
        {
            box.SetValue(IsEndingProperty, false);
        }
    }
}
