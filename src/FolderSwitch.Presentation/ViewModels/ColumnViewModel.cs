using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A vertical stack of groups in a tab. A projection of its <see cref="Column"/>.</summary>
public sealed class ColumnViewModel : ObservableObject
{
    internal ColumnViewModel(Column model, TabViewModel tab, Localizer localizer)
    {
        Model = model;
        Tab = tab;
        Groups = new ObservableCollection<GroupViewModel>(model.Groups.Select(group => new GroupViewModel(group, this, localizer)));
        Groups.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>The tab the column is in.</summary>
    public TabViewModel Tab { get; }

    /// <summary>Fixed width in pixels (150 to 520, step 10); <c>null</c> when the width follows the content.</summary>
    public int? Width => Model.Width;

    public ObservableCollection<GroupViewModel> Groups { get; }

    /// <summary>Only an empty column can be removed.</summary>
    public bool IsEmpty => Groups.Count == 0;

    internal Column Model { get; }

    internal void NotifyWidth() => OnPropertyChanged(nameof(Width));
}
