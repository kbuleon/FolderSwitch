// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FolderSwitch.App.Themes;
using FolderSwitch.App.Views.DragDrop;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// A column of the board, bound to a <see cref="ColumnViewModel"/>. In Customize mode its grip ⠿ starts a drag of the
/// column (<see cref="BoardDragDrop"/>), which the board takes; the column itself takes the groups dropped on it (by
/// their grip), at the slot under the mouse, whatever tab they come from. An item dropped on it outside any group is
/// refused (the groups take the items).
/// </summary>
public partial class ColumnView : UserControl
{
    public static readonly DependencyProperty IsCustomizingProperty = DependencyProperty.Register(
        nameof(IsCustomizing), typeof(bool), typeof(ColumnView), new PropertyMetadata(false, OnContentChanged));

    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance), typeof(object), typeof(ColumnView), new PropertyMetadata(null, OnContentChanged));

    public ColumnView()
    {
        SetResourceReference(AppearanceProperty, ThemeKeys.Appearance);
        SetBinding(IsCustomizingProperty, new Binding($"{nameof(DataContext)}.{nameof(MainViewModel.IsCustomizing)}")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(MainView), 1),
        });
        InitializeComponent();
        Grip.Presenter = WidthPresenter;

        var move = PressGesture.For(MoveGrip);
        move.CanDrag = () => IsCustomizing && DataContext is ColumnViewModel;
        move.Drag = () =>
        {
            if (DataContext is ColumnViewModel column)
            {
                var viewModel = ViewModel;
                BoardDragDrop.Start(this, column);
                viewModel?.EndDrag();
            }
        };
    }

    /// <summary>Whether the window is in Customize mode (bound to the main view model).</summary>
    public bool IsCustomizing
    {
        get => (bool)GetValue(IsCustomizingProperty);
        set => SetValue(IsCustomizingProperty, value);
    }

    /// <summary>The current appearance (a resource): a theme changes what the groups need.</summary>
    public object? Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>The list of the column's groups: where a dragged group goes (the snapshots draw its indicator).</summary>
    internal ItemsControl Groups => GroupsList;

    /// <summary>What lays the column out at its width (the snapshots show a resize in progress through it).</summary>
    internal Layout.ColumnWidthPresenter Presenter => WidthPresenter;

    private MainViewModel? ViewModel => MainView.Of(this)?.DataContext as MainViewModel;

    /// <summary>
    /// Customize mode shows more in every group (grip, dot, ✕, the « + » buttons), and a theme draws them otherwise: an
    /// automatic column fits its content again. WPF alone would measure the content at the column's last width and never
    /// widen it (see <see cref="Layout.ColumnWidthPresenter"/>).
    /// </summary>
    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ColumnView)d).WidthPresenter?.RemeasureAll();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (BoardDragDrop.Payload<ColumnViewModel>(e) is not null)
        {
            // A column: the board's to take.
            return;
        }

        if (BoardDragDrop.Payload<GroupViewModel>(e) is { } group && CanTake(group))
        {
            var slot = BoardDragDrop.SlotAt(GroupsList, e.GetPosition(GroupsList), Orientation.Vertical);
            BoardDragDrop.ShowIndicator(GroupsList, slot, Orientation.Vertical);
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
            BoardDragDrop.HideIndicator();
        }

        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (BoardDragDrop.Payload<ColumnViewModel>(e) is null)
        {
            BoardDragDrop.HideIndicatorIfLeft(this, e);
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (BoardDragDrop.Payload<ColumnViewModel>(e) is not null)
        {
            return;
        }

        e.Handled = true;
        BoardDragDrop.HideIndicator();
        if (BoardDragDrop.Payload<GroupViewModel>(e) is not { } group || !CanTake(group)
            || DataContext is not ColumnViewModel column || ViewModel is not { } viewModel)
        {
            return;
        }

        var slot = BoardDragDrop.SlotAt(GroupsList, e.GetPosition(GroupsList), Orientation.Vertical);
        var source = ReferenceEquals(group.Column, column) ? column.Groups.IndexOf(group) : -1;
        if (!InsertionSlot.IsNoMove(slot, source))
        {
            viewModel.MoveGroup(group, column, InsertionSlot.ToMoveIndex(slot, source));
        }
    }

    /// <summary>Only in Customize mode; a group of any tab.</summary>
    private bool CanTake(GroupViewModel _) => IsCustomizing && DataContext is ColumnViewModel;
}
