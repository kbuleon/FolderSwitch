// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.SpecialFolders;

/// <summary>
/// The well-known Windows locations an item can point to, in the order they are offered:
/// the eight personal folders first, then the eight system ones.
/// </summary>
public static class SpecialFolderCatalog
{
    private const string NameKeyPrefix = "SpecialFolder.";

    public static IReadOnlyList<SpecialFolderInfo> All { get; } = new List<SpecialFolderInfo>
    {
        PersonalEntry(SpecialFolderKey.Desktop, @"%USERPROFILE%\Desktop"),
        PersonalEntry(SpecialFolderKey.Documents, @"%USERPROFILE%\Documents"),
        PersonalEntry(SpecialFolderKey.Downloads, @"%USERPROFILE%\Downloads"),
        PersonalEntry(SpecialFolderKey.Pictures, @"%USERPROFILE%\Pictures"),
        PersonalEntry(SpecialFolderKey.Music, @"%USERPROFILE%\Music"),
        PersonalEntry(SpecialFolderKey.Videos, @"%USERPROFILE%\Videos"),
        PersonalEntry(SpecialFolderKey.OneDrive, "%OneDrive%"),
        PersonalEntry(SpecialFolderKey.UserProfile, "%USERPROFILE%"),
        SystemEntry(SpecialFolderKey.ThisPC, "shell:MyComputerFolder"),
        SystemEntry(SpecialFolderKey.RecycleBin, "shell:RecycleBinFolder"),
        SystemEntry(SpecialFolderKey.ProgramFiles, "%ProgramFiles%"),
        SystemEntry(SpecialFolderKey.RoamingAppData, "%APPDATA%"),
        SystemEntry(SpecialFolderKey.LocalAppData, "%LOCALAPPDATA%"),
        SystemEntry(SpecialFolderKey.Temp, "%TEMP%"),
        SystemEntry(SpecialFolderKey.Startup, "shell:startup"),
        SystemEntry(SpecialFolderKey.Windows, "%windir%"),
    }.AsReadOnly();

    /// <summary>
    /// Localization key of a special folder's display name. Total: an undefined value (from a damaged
    /// configuration file) yields a key too, never an exception.
    /// </summary>
    internal static string NameKeyFor(SpecialFolderKey key) => NameKeyPrefix + key;

    private static SpecialFolderInfo PersonalEntry(SpecialFolderKey key, string storageForm) =>
        new(key, SpecialFolderCategory.Personal, NameKeyFor(key), storageForm);

    private static SpecialFolderInfo SystemEntry(SpecialFolderKey key, string storageForm) =>
        new(key, SpecialFolderCategory.System, NameKeyFor(key), storageForm);
}
