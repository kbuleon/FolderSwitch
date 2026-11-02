// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>A folder or file shortcut, as shown in its group. A projection of its <see cref="Item"/>.</summary>
public sealed class ItemViewModel : RenamableViewModel
{
    private readonly Func<string, string> _localize;
    private bool _isUsable = true;
    private string? _faviconPath;

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

    /// <summary>The greyed line under the name: the path, the readable form of a special folder, or the host of a site.</summary>
    public string SecondaryText => Model.Special is { } special
        ? StorageFormOf(special)
        : IsUrl ? WebAddress.HostLabel(Model.Url ?? string.Empty) : Model.Path ?? string.Empty;

    public bool IsFolder => Model.Kind == ItemKind.Folder;

    public bool IsFile => Model.Kind == ItemKind.File;

    public bool IsUrl => Model.Kind == ItemKind.Url;

    /// <summary>The address of a site (« Copy address »); <c>null</c> for anything else.</summary>
    public string? Url => IsUrl ? Model.Url : null;

    /// <summary>
    /// Whether a click does something in the current call context: a file or a site is not (a file dialog can only be
    /// moved to a folder), unless the board is being customized. Kept up to date by <see cref="MainViewModel"/>.
    /// </summary>
    public bool IsUsable
    {
        get => _isUsable;
        private set => SetProperty(ref _isUsable, value);
    }

    /// <summary>The file of the site's icon in the cache, or <c>null</c> (the globe is shown).</summary>
    public string? FaviconPath
    {
        get => _faviconPath;
        private set => SetProperty(ref _faviconPath, value);
    }

    /// <summary>The extension of a file, with its dot (<c>.xlsx</c>), for the icon of its type; <c>null</c> for anything else or when it has none.</summary>
    public string? Extension => IsFile && Path.GetExtension(Model.Path) is { Length: > 1 } extension ? extension : null;

    internal Item Model { get; }

    internal override Guid Id => Model.Id;

    internal override string CurrentName => DisplayName;

    internal override void NotifyRenamed() => OnPropertyChanged(nameof(DisplayName));

    internal void SetUsable(bool value) => IsUsable = value;

    internal void SetFavicon(string? path) => FaviconPath = path;

    /// <summary>The language changed: a special folder's default name follows it.</summary>
    internal void RefreshTexts() => OnPropertyChanged(nameof(DisplayName));

    // An undefined key (damaged configuration) has no catalog entry: no secondary text rather than an exception.
    private static string StorageFormOf(SpecialFolderKey key) =>
        SpecialFolderCatalog.All.FirstOrDefault(info => info.Key == key)?.StorageForm ?? string.Empty;
}
