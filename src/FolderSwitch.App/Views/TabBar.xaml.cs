// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FolderSwitch.App.Views.DragDrop;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The tab bar, bound to <see cref="MainViewModel"/>: a click shows a tab; in Customize mode it also selects it, a
/// double-click renames it, a right-click shows it and opens its <see cref="EditMenu"/>, and a drag moves it along the bar (<see cref="BoardDragDrop"/>), across its lines when the
/// tabs wrap. A column, group or item dragged onto another tab, and resting there for <see cref="SwitchDelay"/>,
/// shows that tab, so that the drag can end in it; nothing is dropped on the bar itself. The icon button (which opens
/// the icon picker) and ✕ of Customize mode are buttons of their own: a press on them neither shows the tab nor drags it.
/// </summary>
public partial class TabBar : UserControl
{
    /// <summary>How long a drag rests on a tab before the tab is shown.</summary>
    private static readonly TimeSpan SwitchDelay = TimeSpan.FromMilliseconds(500);

    private readonly DispatcherTimer _switchTimer;
    private TabViewModel? _switchTarget;

    public TabBar()
    {
        InitializeComponent();
        _switchTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher) { Interval = SwitchDelay };
        _switchTimer.Tick += OnSwitchTimer;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>Each tab gets its gesture once its view exists.</summary>
    private void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        var surface = (FrameworkElement)sender;
        var gesture = PressGesture.For(surface);
        gesture.Click = _ => OnTabClick(surface);
        gesture.DoubleClick = () => OnTabDoubleClick(surface);
        surface.MouseRightButtonUp += (_, e) => e.Handled = OnTabRightClick(surface);
        gesture.CanDrag = () => ViewModel is { IsCustomizing: true } && surface.DataContext is TabViewModel { IsRenaming: false };
        gesture.Drag = () =>
        {
            if (surface.DataContext is TabViewModel tab)
            {
                BoardDragDrop.Start(surface, tab);
            }
        };
    }

    private void OnTabClick(FrameworkElement surface)
    {
        if (ViewModel is not { } viewModel || surface.DataContext is not TabViewModel tab)
        {
            return;
        }

        viewModel.ActiveTab = tab;
        if (viewModel.IsCustomizing)
        {
            viewModel.Selected = tab;
        }
    }

    /// <summary>In Customize mode, a right-click shows the tab and opens its <see cref="EditMenu"/>.</summary>
    private bool OnTabRightClick(FrameworkElement surface)
    {
        if (ViewModel is not { IsCustomizing: true } viewModel || surface.DataContext is not TabViewModel { IsRenaming: false } tab
            || MainView.Of(this) is not { } view)
        {
            return false;
        }

        viewModel.ActiveTab = tab;
        viewModel.Selected = tab;
        view.OpenEditMenu(tab, surface);
        return true;
    }

    /// <summary>Customize mode: the icon button of a tab opens its icon picker, under it (the tab shown stays).</summary>
    private void OnIconClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TabViewModel tab } button)
        {
            MainView.Of(this)?.OpenIconPicker(tab, button);
        }
    }

    private bool OnTabDoubleClick(FrameworkElement surface)
    {
        if (ViewModel is not { IsCustomizing: true } viewModel || surface.DataContext is not TabViewModel tab)
        {
            return false;
        }

        viewModel.ActiveTab = tab;
        viewModel.BeginRename(tab);
        return true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (ViewModel is { IsCustomizing: true }
            && BoardDragDrop.Payload<object>(e) is (ColumnViewModel or GroupViewModel or ItemViewModel) and var payload)
        {
            // A column, a group or an item: the bar only shows the tab it rests on. The cursor over a tab it will show
            // is the one of a move, as over the board: the drag goes on there.
            BoardDragDrop.HideIndicator();
            e.Effects = ArmSwitch(TabAt(e), payload) ? DragDropEffects.Move : DragDropEffects.None;
            return;
        }

        if (ViewModel is not { IsCustomizing: true } viewModel || BoardDragDrop.Payload<TabViewModel>(e) is not { } tab
            || !viewModel.Tabs.Contains(tab))
        {
            e.Effects = DragDropEffects.None;
            BoardDragDrop.HideIndicator();
            return;
        }

        var position = e.GetPosition(TabsList);
        var slot = BoardDragDrop.SlotAt(TabsList, position, Orientation.Horizontal);
        BoardDragDrop.ShowIndicator(TabsList, slot, Orientation.Horizontal, position);
        e.Effects = DragDropEffects.Move;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        BoardDragDrop.HideIndicatorIfLeft(TabsList, e);
        var position = e.GetPosition(TabsList);
        if (position.X < 0 || position.Y < 0 || position.X > TabsList.ActualWidth || position.Y > TabsList.ActualHeight)
        {
            DisarmSwitch();
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        DisarmSwitch();
        BoardDragDrop.HideIndicator();
        if (ViewModel is not { IsCustomizing: true } viewModel || BoardDragDrop.Payload<TabViewModel>(e) is not { } tab)
        {
            return;
        }

        var slot = BoardDragDrop.SlotAt(TabsList, e.GetPosition(TabsList), Orientation.Horizontal);
        var source = viewModel.Tabs.IndexOf(tab);
        if (source >= 0 && !InsertionSlot.IsNoMove(slot, source))
        {
            viewModel.MoveTab(tab, InsertionSlot.ToMoveIndex(slot, source));
        }
    }

    /// <summary>The tab under the mouse, if any.</summary>
    private TabViewModel? TabAt(DragEventArgs e) =>
        TabsList.InputHitTest(e.GetPosition(TabsList)) is FrameworkElement { DataContext: TabViewModel tab } ? tab : null;

    /// <summary>
    /// Starts the wait before <paramref name="tab"/> is shown, unless the drag already rests on it; false when the tab
    /// will not be shown: over the tab shown, off the tabs, or for a column that cannot leave its tab (its last one).
    /// </summary>
    private bool ArmSwitch(TabViewModel? tab, object payload)
    {
        var wanted = tab is not null && !ReferenceEquals(tab, ViewModel?.ActiveTab)
            && (payload is not ColumnViewModel column || ReferenceEquals(column.Tab, tab) || column.Tab.Columns.Count > 1);
        if (!wanted)
        {
            DisarmSwitch();
            return false;
        }

        if (!ReferenceEquals(tab, _switchTarget))
        {
            _switchTarget = tab;
            _switchTimer.Stop();
            _switchTimer.Start();
        }

        return true;
    }

    private void DisarmSwitch()
    {
        _switchTimer.Stop();
        _switchTarget = null;
    }

    private void OnSwitchTimer(object? sender, EventArgs e)
    {
        var tab = _switchTarget;
        DisarmSwitch();
        if (tab is not null)
        {
            ViewModel?.ShowTabWhileDragging(tab);
        }
    }
}
