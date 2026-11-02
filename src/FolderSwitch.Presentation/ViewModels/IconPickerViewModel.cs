// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using CommunityToolkit.Mvvm.ComponentModel;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Localization;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>The emojis of one category, under its translated title.</summary>
public sealed record IconSection(EmojiCategory Category, string Title, IReadOnlyList<EmojiEntry> Entries);

/// <summary>
/// The icon picker of a tab or group, which the flyout of Customize mode shows (<see cref="MainViewModel.OpenIconPicker"/>):
/// a filter, the choices « Automatic » and « No icon », and the emojis the font can draw, by category. A choice is
/// carried out by <see cref="MainViewModel"/> (it saves) and raises <see cref="Closed"/>. Meant to be used from the UI thread.
/// </summary>
public sealed partial class IconPickerViewModel : ObservableObject
{
    private static readonly IReadOnlySet<string> NoStopWords = new HashSet<string>(StringComparer.Ordinal);

    private readonly MainViewModel _main;
    private readonly RenamableViewModel _target;
    private readonly IconFinder _finder;
    private readonly Localizer _localizer;
    private IReadOnlyList<IconSection>? _sections;
    private IReadOnlyList<EmojiEntry> _results;
    private bool _isFiltering;
    private IconChoice _current;
    private string? _currentEmoji;

    internal IconPickerViewModel(
        MainViewModel main,
        RenamableViewModel target,
        IconFinder finder,
        Localizer localizer,
        IconChoice current,
        string? currentEmoji)
    {
        _main = main;
        _target = target;
        _finder = finder;
        _localizer = localizer;
        _current = current;
        _currentEmoji = currentEmoji;
        _results = finder.Drawable;
        AutomaticIcon = main.AutomaticIcon(target);
    }

    /// <summary>Raised after a choice was made (and saved): the flyout closes.</summary>
    public event EventHandler? Closed;

    /// <summary>The text typed in the filter box.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>The emojis matching <see cref="FilterText"/>, best first; every drawable emoji without a filter.</summary>
    public IReadOnlyList<EmojiEntry> Results => _results;

    /// <summary>True when the filter has words: the grid then shows <see cref="Results"/> instead of <see cref="Sections"/>.</summary>
    public bool IsFiltering => _isFiltering;

    /// <summary>The drawable emojis by category, in the order of the categories, without the empty ones.</summary>
    public IReadOnlyList<IconSection> Sections => _sections ??= BuildSections();

    /// <summary>The emoji the name of the target suggests, shown on the « Automatic » choice; <c>null</c> for none.</summary>
    public string? AutomaticIcon { get; }

    /// <summary>The choice in force: marked in the picker.</summary>
    public IconChoice Current => _current;

    /// <summary>The emoji in force when <see cref="Current"/> is <see cref="IconChoice.Emoji"/>; otherwise <c>null</c>.</summary>
    public string? CurrentEmoji => _currentEmoji;

    /// <summary>The name of <paramref name="emoji"/> in the language of the application (English when it has none there), for its tooltip.</summary>
    public string? NameOf(string emoji) => _finder.NameOf(emoji, _localizer.Code);

    /// <summary>Gives the target this emoji, as a chosen icon, and closes the picker.</summary>
    public void Choose(string emoji)
    {
        ArgumentNullException.ThrowIfNull(emoji);

        _main.SetChosenIcon(_target, emoji);
        Close(IconChoice.Emoji, emoji);
    }

    /// <summary>Gives the target no icon, by choice, and closes the picker.</summary>
    public void ChooseNone()
    {
        _main.SetNoIcon(_target);
        Close(IconChoice.None, null);
    }

    /// <summary>Makes the icon of the target automatic again and closes the picker.</summary>
    public void ChooseAutomatic()
    {
        _main.SetAutomaticIcon(_target);
        Close(IconChoice.Automatic, null);
    }

    /// <summary>Chooses the first of <see cref="Results"/> (Enter in the filter box); <c>false</c>, and nothing changes, without a result.</summary>
    public bool ChooseFirstResult()
    {
        if (_results.Count == 0)
        {
            return false;
        }

        Choose(_results[0].Emoji);
        return true;
    }

    partial void OnFilterTextChanged(string value)
    {
        _isFiltering = IconWords.Split(value ?? string.Empty, NoStopWords).Count > 0;
        _results = _isFiltering ? _finder.Filter(value!, _localizer.Code) : _finder.Drawable;
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(IsFiltering));
    }

    private void Close(IconChoice current, string? emoji)
    {
        _current = current;
        _currentEmoji = emoji;
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(CurrentEmoji));
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<IconSection> BuildSections() =>
        _finder.Drawable
            .GroupBy(entry => entry.Category)
            .OrderBy(group => group.Key)
            .Select(group => new IconSection(group.Key, _localizer["Icon.Category." + group.Key], group.ToList().AsReadOnly()))
            .ToList()
            .AsReadOnly();
}
