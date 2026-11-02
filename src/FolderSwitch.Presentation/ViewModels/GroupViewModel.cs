// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.ObjectModel;
using FolderSwitch.Core.Editing;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A named, colored, collapsible block of items. A projection of its <see cref="Group"/>.</summary>
public sealed class GroupViewModel : RenamableViewModel
{
    private readonly Localizer _localizer;
    private IReadOnlyList<SpecialFolderState>? _specialFolderStates;

    internal GroupViewModel(Group model, ColumnViewModel column, Localizer localizer)
    {
        Model = model;
        Column = column;
        _localizer = localizer;
        Items = new ObservableCollection<ItemViewModel>(model.Items.Select(item => new ItemViewModel(item, this, localizer)));
        Items.CollectionChanged += (_, _) => OnItemsChanged();
    }

    /// <summary>The column the group is in.</summary>
    public ColumnViewModel Column { get; internal set; }

    public string Name => Model.Name;

    /// <summary>Header color, <c>#rrggbb</c>.</summary>
    public string Color => Model.Color;

    public bool IsCollapsed => Model.Collapsed;

    /// <summary>Number of items, shown in the header while the group is collapsed.</summary>
    public int Count => Items.Count;

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
