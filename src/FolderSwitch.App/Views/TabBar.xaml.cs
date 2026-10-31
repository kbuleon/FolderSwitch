using System.Windows;
using System.Windows.Controls;
using FolderSwitch.App.Views.DragDrop;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The tab bar, bound to <see cref="MainViewModel"/>: a click shows a tab; in Customize mode it also selects it, a
/// double-click renames it and a drag moves it along the bar (<see cref="BoardDragDrop"/>), across its lines when the
/// tabs wrap.
/// </summary>
public partial class TabBar : UserControl
{
    public TabBar()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>Each tab gets its gesture once its view exists.</summary>
    private void OnTabLoaded(object sender, RoutedEventArgs e)
    {
        var surface = (FrameworkElement)sender;
        var gesture = PressGesture.For(surface);
        gesture.Click = _ => OnTabClick(surface);
        gesture.DoubleClick = () => OnTabDoubleClick(surface);
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

    private void OnDragLeave(object sender, DragEventArgs e) => BoardDragDrop.HideIndicatorIfLeft(TabsList, e);

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
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
}
