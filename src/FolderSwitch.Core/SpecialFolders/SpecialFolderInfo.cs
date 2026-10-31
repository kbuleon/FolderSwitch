using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.SpecialFolders;

/// <summary>Static description of one entry of the <see cref="SpecialFolderCatalog"/>.</summary>
/// <param name="Key">The folder this entry describes.</param>
/// <param name="Category">The group the folder is listed under.</param>
/// <param name="NameKey">Localization key of the folder's display name, <c>SpecialFolder.&lt;Key&gt;</c>.</param>
/// <param name="StorageForm">
/// Readable form of where the folder lives (environment variable or <c>shell:</c> moniker).
/// Documentation only: the configuration stores the <see cref="SpecialFolderKey"/>, never this text.
/// </param>
public sealed record SpecialFolderInfo(
    SpecialFolderKey Key,
    SpecialFolderCategory Category,
    string NameKey,
    string StorageForm);
