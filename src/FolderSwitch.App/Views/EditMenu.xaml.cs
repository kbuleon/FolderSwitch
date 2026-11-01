using System.Windows;
using System.Windows.Controls;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The menu of a tab, a group or an item in Edit mode: rename it, as F2 does (<see cref="MainViewModel.BeginRename"/>).
/// A choice closes the menu first.
/// </summary>
public partial class EditMenu : UserControl
{
    private readonly MainViewModel _main;
    private readonly RenamableViewModel _target;
    private readonly Action _close;

    internal EditMenu(MainViewModel main, RenamableViewModel target, ViewTexts texts, Action close)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        _main = main;
        _target = target;
        _close = close;
        Texts = texts;
        InitializeComponent();
    }

    public ViewTexts Texts { get; }

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        _close();
        _main.BeginRename(_target);
    }
}
