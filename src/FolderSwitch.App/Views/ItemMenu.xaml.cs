using System.Windows;
using System.Windows.Controls;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The menu of an item in normal use: open it in File Explorer, or in a new tab as Ctrl+click does
/// (<see cref="MainViewModel.CanOpenInNewTab"/>). Built when it opens, for the calling window of that moment. A choice
/// closes the menu first, then is carried out by the view, as a click on the item is.
/// </summary>
public partial class ItemMenu : UserControl
{
    private readonly ItemViewModel _item;
    private readonly Action _close;
    private readonly Action<ItemViewModel> _openInExplorer;
    private readonly Action<ItemViewModel> _openInNewTab;

    internal ItemMenu(
        MainViewModel main,
        ItemViewModel item,
        ViewTexts texts,
        Action close,
        Action<ItemViewModel> openInExplorer,
        Action<ItemViewModel> openInNewTab)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(openInExplorer);
        ArgumentNullException.ThrowIfNull(openInNewTab);

        _item = item;
        _close = close;
        _openInExplorer = openInExplorer;
        _openInNewTab = openInNewTab;
        Texts = texts;

        // A file opens its location: its folder, as Windows' own « Open file location ».
        ExplorerLabel = texts[item.IsFolder ? "Item.OpenInExplorer" : "Item.OpenFileLocation"];

        InitializeComponent();
        NewTabEntry.Visibility = main.CanOpenInNewTab(item) ? Visibility.Visible : Visibility.Collapsed;
    }

    public ViewTexts Texts { get; }

    /// <summary>« Open in File Explorer », or « Open file location » for a file.</summary>
    public string ExplorerLabel { get; }

    /// <summary>Whether « Open in new tab » is offered.</summary>
    internal bool OffersNewTab => NewTabEntry.Visibility == Visibility.Visible;

    private void OnExplorerClick(object sender, RoutedEventArgs e)
    {
        _close();
        _openInExplorer(_item);
    }

    private void OnNewTabClick(object sender, RoutedEventArgs e)
    {
        _close();
        _openInNewTab(_item);
    }
}
