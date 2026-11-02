// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using FolderSwitch.App.Views.DragDrop;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The board of a tab, bound to a <see cref="TabViewModel"/>. In Customize mode it takes the columns dropped on it (by
/// their grip, <see cref="BoardDragDrop"/>), at the slot under the mouse, from its tab or from another one (shown by
/// the tab bar during the drag); the columns pass a dragged column on to it.
/// </summary>
public partial class BoardView : UserControl
{
    public BoardView()
    {
        InitializeComponent();
    }

    /// <summary>The list of the tab's columns: where a dragged column goes (the snapshots draw its indicator).</summary>
    internal ItemsControl Columns => ColumnsList;

    private MainViewModel? ViewModel => MainView.Of(this)?.DataContext as MainViewModel;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (BoardDragDrop.Payload<ColumnViewModel>(e) is not { } column || !CanTake(column))
        {
            e.Effects = DragDropEffects.None;
            BoardDragDrop.HideIndicator();
            return;
        }

        var position = e.GetPosition(ColumnsList);
        var slot = BoardDragDrop.SlotAt(ColumnsList, position, Orientation.Horizontal);
        BoardDragDrop.ShowIndicator(ColumnsList, slot, Orientation.Horizontal, position);
        e.Effects = DragDropEffects.Move;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => BoardDragDrop.HideIndicatorIfLeft(this, e);

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        BoardDragDrop.HideIndicator();
        if (BoardDragDrop.Payload<ColumnViewModel>(e) is not { } column || !CanTake(column) || ViewModel is not { } viewModel
            || DataContext is not TabViewModel target)
        {
            return;
        }

        var slot = BoardDragDrop.SlotAt(ColumnsList, e.GetPosition(ColumnsList), Orientation.Horizontal);
        var source = target.Columns.IndexOf(column);
        if (!InsertionSlot.IsNoMove(slot, source))
        {
            viewModel.MoveColumn(column, target, InsertionSlot.ToMoveIndex(slot, source));
        }
    }

    /// <summary>Only in Customize mode; a column of another tab only if it is not the last one of its tab.</summary>
    private bool CanTake(ColumnViewModel column) =>
        ViewModel is { IsCustomizing: true } && DataContext is TabViewModel tab
        && (ReferenceEquals(column.Tab, tab) || column.Tab.Columns.Count > 1);
}
