using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>One special folder of the « + Folder ▾ » menu.</summary>
/// <param name="Key">The folder.</param>
/// <param name="Name">Its name in the current language.</param>
/// <param name="StorageForm">Where it lives, as Windows writes it (<c>%USERPROFILE%\Desktop</c>), in grey.</param>
/// <param name="AlreadyPresent">The group holds it already: greyed out and checked.</param>
/// <param name="ToolTip">Why it is greyed out; <c>null</c> otherwise.</param>
public sealed record SpecialFolderEntry(SpecialFolderKey Key, string Name, string StorageForm, bool AlreadyPresent, string? ToolTip);

/// <summary>
/// The « + Folder ▾ » menu of a group, built from <see cref="GroupViewModel.SpecialFolderStates"/> when it opens (the
/// group cannot change while it is open). « Browse… » closes it before the picker opens: the picker must not show
/// under the menu.
/// </summary>
public partial class AddFolderMenu : UserControl
{
    private readonly MainViewModel _main;
    private readonly GroupViewModel _group;
    private readonly Action _close;

    internal AddFolderMenu(MainViewModel main, GroupViewModel group, ViewTexts texts, Action close)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        _main = main;
        _group = group;
        _close = close;
        Texts = texts;

        var entries = group.SpecialFolderStates
            .Select(state => (state.Info.Category, Entry: new SpecialFolderEntry(
                state.Info.Key,
                state.Name,
                state.Info.StorageForm,
                state.AlreadyPresent,
                state.AlreadyPresent ? texts["Item.AlreadyPresent"] : null)))
            .ToList();
        PersonalFolders = entries.Where(e => e.Category == SpecialFolderCategory.Personal).Select(e => e.Entry).ToList();
        SystemFolders = entries.Where(e => e.Category == SpecialFolderCategory.System).Select(e => e.Entry).ToList();

        InitializeComponent();
    }

    public ViewTexts Texts { get; }

    public IReadOnlyList<SpecialFolderEntry> PersonalFolders { get; }

    public IReadOnlyList<SpecialFolderEntry> SystemFolders { get; }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        _close();

        // Once the menu is gone: the picker is modal.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (_main.AddFolderCommand.CanExecute(_group))
            {
                _main.AddFolderCommand.Execute(_group);
            }
        });
    }

    private void OnSpecialClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SpecialFolderEntry { AlreadyPresent: false } entry })
        {
            _close();
            _main.AddSpecialFolder(_group, entry.Key);
        }
    }
}
