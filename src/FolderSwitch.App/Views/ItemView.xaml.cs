// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderSwitch.App.Views.DragDrop;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// An item of a group, bound to an <see cref="ItemViewModel"/>. In normal use a click opens it and a right-click opens
/// its menu (<see cref="ItemMenu"/>), unless the calling window cannot use it (<see cref="ItemViewModel.IsUsable"/>):
/// then neither does anything, and only its tooltip shows. In Customize mode a click selects it, a double-click renames
/// it and a drag moves it (<see cref="BoardDragDrop"/>).
/// </summary>
public partial class ItemView : UserControl
{
    public ItemView()
    {
        InitializeComponent();

        var gesture = PressGesture.For(Row);
        gesture.Click = _ => OnClick();
        gesture.DoubleClick = BeginRename;
        gesture.CanDrag = () => ViewModel is { IsCustomizing: true } && DataContext is ItemViewModel { IsRenaming: false };
        gesture.Drag = () =>
        {
            if (DataContext is ItemViewModel item)
            {
                var viewModel = ViewModel;
                BoardDragDrop.Start(this, item);
                viewModel?.EndDrag();
            }
        };

        Row.MouseRightButtonUp += OnRightClick;
    }

    /// <summary>What the item's menu opens under.</summary>
    internal FrameworkElement MenuAnchor => Row;

    private MainViewModel? ViewModel => MainView.Of(this)?.DataContext as MainViewModel;

    /// <summary>
    /// A right-click opens the item's menu: <see cref="ItemMenu"/> in normal use, <see cref="EditMenu"/> in Customize
    /// mode (where it also selects the item).
    /// </summary>
    private void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ItemViewModel item || MainView.Of(this) is not { } view)
        {
            return;
        }

        if (view.DataContext is MainViewModel { IsCustomizing: true } viewModel)
        {
            if (!item.IsRenaming)
            {
                viewModel.Selected = item;
                view.OpenEditMenu(item, Row);
            }
        }
        else if (item.IsUsable)
        {
            view.OpenItemMenu(item, Row);
        }

        e.Handled = true;
    }

    /// <summary>A click opens the item; with Ctrl held, in a new tab or window (spec 5.3). In Customize mode it selects it.</summary>
    private void OnClick()
    {
        if (DataContext is not ItemViewModel item || MainView.Of(this) is not { } view)
        {
            return;
        }

        if (view.DataContext is MainViewModel { IsCustomizing: true } viewModel)
        {
            viewModel.Selected = item;
            return;
        }

        if (item.IsUsable)
        {
            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            view.ActivateItem(item, ctrl);
        }
    }

    private bool BeginRename()
    {
        if (ViewModel is not { IsCustomizing: true } viewModel || DataContext is not ItemViewModel item)
        {
            return false;
        }

        viewModel.BeginRename(item);
        return true;
    }
}
