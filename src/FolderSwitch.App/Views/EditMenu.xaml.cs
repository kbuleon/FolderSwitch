// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The menu of a tab, a group or an item in Edit mode: rename it, as F2 does (<see cref="MainViewModel.BeginRename"/>);
/// for a tab or a group, choose its icon (the icon picker, opened by the window). A separator has no name: its menu
/// removes it instead (<see cref="MainViewModel.RemoveItem"/>). A choice closes the menu first; the picker opens once
/// the menu is gone, in the same flyout.
/// </summary>
public partial class EditMenu : UserControl
{
    private readonly MainViewModel _main;
    private readonly RenamableViewModel _target;
    private readonly Action _close;
    private readonly Action<RenamableViewModel> _openIconPicker;

    internal EditMenu(MainViewModel main, RenamableViewModel target, ViewTexts texts, Action close, Action<RenamableViewModel> openIconPicker)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);
        ArgumentNullException.ThrowIfNull(openIconPicker);

        _main = main;
        _target = target;
        _close = close;
        _openIconPicker = openIconPicker;
        Texts = texts;
        InitializeComponent();
        IconEntry.Visibility = target is TabViewModel or GroupViewModel ? Visibility.Visible : Visibility.Collapsed;

        var separator = target is ItemViewModel { IsSeparator: true };
        RenameEntry.Visibility = separator ? Visibility.Collapsed : Visibility.Visible;
        RemoveEntry.Visibility = separator ? Visibility.Visible : Visibility.Collapsed;
    }

    public ViewTexts Texts { get; }

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        _close();
        _main.BeginRename(_target);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        _close();
        if (_target is ItemViewModel item)
        {
            _main.RemoveItem(item);
        }
    }

    private void OnIconClick(object sender, RoutedEventArgs e)
    {
        var target = _target;
        _close();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _openIconPicker(target));
    }
}
