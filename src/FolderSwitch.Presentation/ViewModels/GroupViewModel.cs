// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.ObjectModel;
using FolderSwitch.Core.Editing;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A named, colored, collapsible block of items. A projection of its <see cref="Group"/>.</summary>
public sealed class GroupViewModel : RenamableViewModel
{
    private readonly Localizer _localizer;
    private readonly Lazy<IconFinder> _icons;
    private IReadOnlyList<SpecialFolderState>? _specialFolderStates;

    internal GroupViewModel(Group model, ColumnViewModel column, Localizer localizer, Lazy<IconFinder> icons)
    {
        Model = model;
        Column = column;
        _localizer = localizer;
        _icons = icons;
        Items = new ObservableCollection<ItemViewModel>(model.Items.Select(item => new ItemViewModel(item, this, localizer)));
        Items.CollectionChanged += (_, _) => OnItemsChanged();
    }

    /// <summary>The column the group is in.</summary>
    public ColumnViewModel Column { get; internal set; }

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

    /// <summary>Header color, <c>#rrggbb</c>.</summary>
    public string Color => Model.Color;

    public bool IsCollapsed => Model.Collapsed;

    /// <summary>
    /// Number of items, shown in the header while the group is collapsed and in Customize mode; separators are not
    /// counted.
    /// </summary>
    public int Count => Items.Count(item => !item.IsSeparator);

    public ObservableCollection<ItemViewModel> Items { get; }

    /// <summary>
    /// Every special folder of the catalog, in catalog order, with whether the group already holds it.
    /// Built when first read (the menu is rarely opened) and rebuilt after the items or the language change.
    /// </summary>
    public IReadOnlyList<SpecialFolderState> SpecialFolderStates => _specialFolderStates ??= BuildSpecialFolderStates();

    internal Group Model { get; }

    internal override Guid Id => Model.Id;

    internal override string CurrentName => Name;

    internal override void NotifyRenamed() => OnPropertyChanged(nameof(Name));

    /// <summary>The icon of the model changed: raises the change notifications of the icon.</summary>
    internal void NotifyIcon()
    {
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(HasIcon));
        OnPropertyChanged(nameof(IconChosen));
    }

    internal void NotifyCollapsed() => OnPropertyChanged(nameof(IsCollapsed));

    internal void NotifyColor() => OnPropertyChanged(nameof(Color));

    /// <summary>The language changed: default item names and special folder names follow it.</summary>
    internal void RefreshTexts()
    {
        foreach (var item in Items)
        {
            item.RefreshTexts();
        }

        InvalidateSpecialFolderStates();
    }

    private void OnItemsChanged()
    {
        OnPropertyChanged(nameof(Count));
        InvalidateSpecialFolderStates();
    }

    private void InvalidateSpecialFolderStates()
    {
        _specialFolderStates = null;
        OnPropertyChanged(nameof(SpecialFolderStates));
    }

    private IReadOnlyList<SpecialFolderState> BuildSpecialFolderStates() =>
        SpecialFolderCatalog.All
            .Select(info => new SpecialFolderState(
                info,
                _localizer[info.NameKey],
                BoardEditor.ContainsTarget(Model, new Item { Kind = ItemKind.Folder, Special = info.Key })))
            .ToList()
            .AsReadOnly();
}
