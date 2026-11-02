// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The menu of an item in normal use: open it in File Explorer, or in a new tab as Ctrl+click does
/// (<see cref="MainViewModel.CanOpenInNewTab"/>); for a site, copy its address to the clipboard, and nothing else.
/// Built when it opens, for the calling window of that moment. A choice closes the menu first, then is carried out by
/// the view, as a click on the item is.
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
        CopyAddressEntry.Visibility = item.IsUrl ? Visibility.Visible : Visibility.Collapsed;
        ExplorerEntry.Visibility = item.IsUrl ? Visibility.Collapsed : Visibility.Visible;
        NewTabEntry.Visibility = !item.IsUrl && main.CanOpenInNewTab(item) ? Visibility.Visible : Visibility.Collapsed;
    }

    public ViewTexts Texts { get; }

    /// <summary>« Open in File Explorer », or « Open file location » for a file.</summary>
    public string ExplorerLabel { get; }

    /// <summary>Whether « Open in new tab » is offered.</summary>
    internal bool OffersNewTab => NewTabEntry.Visibility == Visibility.Visible;

    /// <summary>The clipboard may be held by another application for a moment: then nothing is copied, and the log says so.</summary>
    private void OnCopyAddressClick(object sender, RoutedEventArgs e)
    {
        _close();
        if (_item.Url is not { } url)
        {
            return;
        }

        try
        {
            Clipboard.SetText(url);
        }
        catch (Exception exception) when (exception is ExternalException or InvalidOperationException)
        {
            DebugLog.Warn($"The address could not be copied: {exception.Message}");
        }
    }

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
