// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.ObjectModel;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A tab of the board, with its columns. A projection of its <see cref="Tab"/>.</summary>
public sealed class TabViewModel : RenamableViewModel
{
    private readonly Lazy<IconFinder> _icons;

    internal TabViewModel(Tab model, Localizer localizer, Lazy<IconFinder> icons)
    {
        Model = model;
        _icons = icons;
        Columns = new ObservableCollection<ColumnViewModel>(model.Columns.Select(column => new ColumnViewModel(column, this, localizer, icons)));
        Columns.CollectionChanged += (_, _) =>
        {
            // Whether a column can be removed depends on how many columns its tab has.
            foreach (var column in Columns)
            {
                column.NotifyRemovable();
            }
        };
    }

    public string Name => Model.Name;

    /// <summary>
    /// The emoji shown before the name: the one the model holds when the emoji font can draw it, otherwise
    /// <c>null</c> (the model keeps it). Reads the emoji data only when the model has an icon.
    /// </summary>
    public string? Icon => Model.Icon is { } icon && _icons.Value.CanShow(icon) ? icon : null;

    /// <summary>True when <see cref="Icon"/> is shown.</summary>
    public bool HasIcon => Icon is not null;

    /// <summary>True when the user chose the icon (an emoji, or none): a rename leaves it alone.</summary>
    public bool IconChosen => Model.IconChosen;

    public ObservableCollection<ColumnViewModel> Columns { get; }

    internal Tab Model { get; }

    internal override Guid Id => Model.Id;

    internal override string CurrentName => Name;

    internal bool HasGroups => Model.Columns.Any(column => column.Groups.Count > 0);

    internal override void NotifyRenamed() => OnPropertyChanged(nameof(Name));

    /// <summary>The icon of the model changed: raises the change notifications of the icon.</summary>
    internal void NotifyIcon()
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(IconChosen));
    }

    /// <summary>The language changed: refreshes every translated text of the tab.</summary>
    internal void RefreshTexts()
    {
        foreach (var group in Columns.SelectMany(column => column.Groups))
        {
            group.RefreshTexts();
        }
    }
}
