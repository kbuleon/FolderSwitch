using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A folder or file shortcut, as shown in its group. A projection of its <see cref="Item"/>.</summary>
public sealed class ItemViewModel : RenamableViewModel
{
    private readonly Func<string, string> _localize;

    internal ItemViewModel(Item model, GroupViewModel group, Localizer localizer)
    {
        Model = model;
        Group = group;
        _localize = key => localizer[key];
    }

    /// <summary>The group the item is in.</summary>
    public GroupViewModel Group { get; internal set; }

    /// <summary>The user's name, else the translated special folder name, else the last segment of the path.</summary>
    public string DisplayName => ItemNaming.DisplayName(Model, _localize);

    /// <summary>The greyed line under the name: the path, or the readable form of a special folder.</summary>
    public string SecondaryText => Model.Special is { } special ? StorageFormOf(special) : Model.Path ?? string.Empty;

    public bool IsFolder => Model.Kind == ItemKind.Folder;

    internal Item Model { get; }

    internal override Guid Id => Model.Id;

    internal override string CurrentName => DisplayName;

    internal override void NotifyRenamed() => OnPropertyChanged(nameof(DisplayName));

    /// <summary>The language changed: a special folder's default name follows it.</summary>
    internal void RefreshTexts() => OnPropertyChanged(nameof(DisplayName));

    // An undefined key (damaged configuration) has no catalog entry: no secondary text rather than an exception.
    private static string StorageFormOf(SpecialFolderKey key) =>
        SpecialFolderCatalog.All.FirstOrDefault(info => info.Key == key)?.StorageForm ?? string.Empty;
}
