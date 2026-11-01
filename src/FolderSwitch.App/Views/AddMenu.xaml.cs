using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>One special folder of the « Add ▾ » menu.</summary>
/// <param name="Key">The folder.</param>
/// <param name="Name">Its name in the current language.</param>
/// <param name="StorageForm">Where it lives, as Windows writes it (<c>%USERPROFILE%\Desktop</c>), in grey.</param>
/// <param name="AlreadyPresent">The group holds it already: greyed out and checked.</param>
/// <param name="ToolTip">Why it is greyed out; <c>null</c> otherwise.</param>
public sealed record SpecialFolderEntry(SpecialFolderKey Key, string Name, string StorageForm, bool AlreadyPresent, string? ToolTip);

/// <summary>
/// The « Add ▾ » menu of a group, on its <see cref="AddMenuViewModel"/>, which it disposes with itself (the flyout
/// disposes what it showed once it closes). The special folders are listed when it opens (the group cannot change while
/// it is open).
/// </summary>
/// <remarks>
/// <para>
/// What adds at once (the current folder or page, a special folder) is carried out first, then the menu closes: the
/// menu's view model holds what was read, and a disposed one does nothing.
/// </para>
/// <para>
/// « Folder… », « File… » and « URL… » open a modal window (a picker, the address prompt). The menu closes first, and
/// the window opens once it is gone, from the main view model: a popup stays above a modal window, and closing it
/// from the modal window's own message loop would tear it down under its click handler. The main view model ignores
/// the addition when Customize mode ended, or the group left the board, in between.
/// </para>
/// </remarks>
public partial class AddMenu : UserControl, IDisposable
{
    private readonly AddMenuViewModel _menu;
    private readonly MainViewModel _main;
    private readonly GroupViewModel _group;
    private readonly Action _close;

    internal AddMenu(AddMenuViewModel menu, MainViewModel main, GroupViewModel group, ViewTexts texts, Action close)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        _menu = menu;
        _main = main;
        _group = group;
        _close = close;
        Texts = texts;

        var entries = menu.SpecialFolders
            .Select(state => (state.Info.Category, Entry: new SpecialFolderEntry(
                state.Info.Key,
                state.Name,
                state.Info.StorageForm,
                state.AlreadyPresent,
                state.AlreadyPresent ? texts["Item.AlreadyPresent"] : null)))
            .ToList();
        PersonalFolders = entries.Where(e => e.Category == SpecialFolderCategory.Personal).Select(e => e.Entry).ToList();
        SystemFolders = entries.Where(e => e.Category == SpecialFolderCategory.System).Select(e => e.Entry).ToList();

        DataContext = menu;
        InitializeComponent();
    }

    public ViewTexts Texts { get; }

    public IReadOnlyList<SpecialFolderEntry> PersonalFolders { get; }

    public IReadOnlyList<SpecialFolderEntry> SystemFolders { get; }

    /// <summary>The menu's view model (the snapshots wait for its page).</summary>
    internal AddMenuViewModel ViewModel => _menu;

    /// <summary>Shows the special folders in place of the menu, or the menu again.</summary>
    internal void ShowSpecialFolders(bool show)
    {
        MainPage.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        SpecialPage.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Ends the menu's view model: a page still being read is dropped.</summary>
    public void Dispose() => _menu.Dispose();

    private void RunThenClose(Action choice)
    {
        choice();
        _close();
    }

    /// <summary>Closes the menu, then opens the modal window of <paramref name="choice"/> once the menu is gone.</summary>
    private void CloseThenAsk(Action<GroupViewModel> choice)
    {
        var group = _group;
        _close();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => choice(group));
    }

    private void OnCurrentClick(object sender, RoutedEventArgs e) => RunThenClose(_menu.AddCurrent);

    private void OnFolderClick(object sender, RoutedEventArgs e) => CloseThenAsk(_main.AddFolder);

    private void OnSpecialFoldersClick(object sender, RoutedEventArgs e) => ShowSpecialFolders(true);

    private void OnBackClick(object sender, RoutedEventArgs e) => ShowSpecialFolders(false);

    private void OnFileClick(object sender, RoutedEventArgs e) => CloseThenAsk(_main.AddFile);

    private void OnUrlClick(object sender, RoutedEventArgs e) => CloseThenAsk(_main.AddUrl);

    private void OnSpecialClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SpecialFolderEntry { AlreadyPresent: false } entry })
        {
            RunThenClose(() => _menu.AddSpecialFolder(entry.Key));
        }
    }
}
