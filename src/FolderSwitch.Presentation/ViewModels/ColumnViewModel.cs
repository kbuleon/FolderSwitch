// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Layout;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A vertical stack of groups in a tab. A projection of its <see cref="Column"/>.</summary>
public sealed class ColumnViewModel : ObservableObject
{
    internal ColumnViewModel(Column model, TabViewModel tab, Localizer localizer, Lazy<IconFinder> icons)
    {
        Model = model;
        Tab = tab;
        Groups = new ObservableCollection<GroupViewModel>(model.Groups.Select(group => new GroupViewModel(group, this, localizer, icons)));
        Groups.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            NotifyRemovable();
        };
    }

    /// <summary>The tab the column is in.</summary>
    public TabViewModel Tab { get; internal set; }

    /// <summary>Width in pixels (150 to 520, step 10).</summary>
    public int Width => Model.Width ?? ColumnWidths.Default;

    public ObservableCollection<GroupViewModel> Groups { get; }

    public bool IsEmpty => Groups.Count == 0;

    /// <summary>Only an empty column can be removed, and a tab keeps at least one column.</summary>
    public bool IsRemovable => IsEmpty && Tab.Columns.Count > 1;

    internal Column Model { get; }

    internal void NotifyWidth() => OnPropertyChanged(nameof(Width));

    internal void NotifyRemovable() => OnPropertyChanged(nameof(IsRemovable));
}
