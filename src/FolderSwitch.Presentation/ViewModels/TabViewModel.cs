using System.Collections.ObjectModel;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A tab of the board, with its columns. A projection of its <see cref="Tab"/>.</summary>
public sealed class TabViewModel : RenamableViewModel
{
    internal TabViewModel(Tab model, Localizer localizer)
    {
        Model = model;
        Columns = new ObservableCollection<ColumnViewModel>(model.Columns.Select(column => new ColumnViewModel(column, this, localizer)));
    }

    public string Name => Model.Name;

    public ObservableCollection<ColumnViewModel> Columns { get; }

    internal Tab Model { get; }

    internal override Guid Id => Model.Id;

    internal override string CurrentName => Name;

    internal bool HasGroups => Model.Columns.Any(column => column.Groups.Count > 0);

    internal override void NotifyRenamed() => OnPropertyChanged(nameof(Name));

    /// <summary>The language changed: refreshes every translated text of the tab.</summary>
    internal void RefreshTexts()
    {
        foreach (var group in Columns.SelectMany(column => column.Groups))
        {
            group.RefreshTexts();
        }
    }
}
