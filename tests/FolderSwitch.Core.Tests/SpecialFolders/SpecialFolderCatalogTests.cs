// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;

namespace FolderSwitch.Core.Tests.SpecialFolders;

public class SpecialFolderCatalogTests
{
    [Fact]
    public void Catalog_has_16_entries_in_spec_order()
    {
        var expected = new (SpecialFolderKey Key, SpecialFolderCategory Category, string StorageForm)[]
        {
            (SpecialFolderKey.Desktop, SpecialFolderCategory.Personal, @"%USERPROFILE%\Desktop"),
            (SpecialFolderKey.Documents, SpecialFolderCategory.Personal, @"%USERPROFILE%\Documents"),
            (SpecialFolderKey.Downloads, SpecialFolderCategory.Personal, @"%USERPROFILE%\Downloads"),
            (SpecialFolderKey.Pictures, SpecialFolderCategory.Personal, @"%USERPROFILE%\Pictures"),
            (SpecialFolderKey.Music, SpecialFolderCategory.Personal, @"%USERPROFILE%\Music"),
            (SpecialFolderKey.Videos, SpecialFolderCategory.Personal, @"%USERPROFILE%\Videos"),
            (SpecialFolderKey.OneDrive, SpecialFolderCategory.Personal, "%OneDrive%"),
            (SpecialFolderKey.UserProfile, SpecialFolderCategory.Personal, "%USERPROFILE%"),
            (SpecialFolderKey.ThisPC, SpecialFolderCategory.System, "shell:MyComputerFolder"),
            (SpecialFolderKey.RecycleBin, SpecialFolderCategory.System, "shell:RecycleBinFolder"),
            (SpecialFolderKey.ProgramFiles, SpecialFolderCategory.System, "%ProgramFiles%"),
            (SpecialFolderKey.RoamingAppData, SpecialFolderCategory.System, "%APPDATA%"),
            (SpecialFolderKey.LocalAppData, SpecialFolderCategory.System, "%LOCALAPPDATA%"),
            (SpecialFolderKey.Temp, SpecialFolderCategory.System, "%TEMP%"),
            (SpecialFolderKey.Startup, SpecialFolderCategory.System, "shell:startup"),
            (SpecialFolderKey.Windows, SpecialFolderCategory.System, "%windir%"),
        };

        var actual = SpecialFolderCatalog.All
            .Select(info => (info.Key, info.Category, info.StorageForm))
            .ToArray();

        Assert.Equal(16, SpecialFolderCatalog.All.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Catalog_keys_are_unique()
    {
        var keys = SpecialFolderCatalog.All.Select(info => info.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Catalog_covers_every_special_folder_key()
    {
        var catalogKeys = SpecialFolderCatalog.All.Select(info => info.Key).OrderBy(key => key);
        var enumKeys = Enum.GetValues<SpecialFolderKey>().OrderBy(key => key);

        Assert.Equal(enumKeys, catalogKeys);
    }

    [Fact]
    public void Name_keys_are_special_folder_prefixed_key_names()
    {
        foreach (var info in SpecialFolderCatalog.All)
        {
            Assert.Equal("SpecialFolder." + info.Key, info.NameKey);
        }

        Assert.Equal(
            "SpecialFolder.ThisPC",
            SpecialFolderCatalog.All.Single(info => info.Key == SpecialFolderKey.ThisPC).NameKey);
    }

    [Fact]
    public void Catalog_lists_the_eight_personal_folders_before_the_eight_system_ones()
    {
        var categories = SpecialFolderCatalog.All.Select(info => info.Category).ToList();

        Assert.Equal(Enumerable.Repeat(SpecialFolderCategory.Personal, 8), categories.Take(8));
        Assert.Equal(Enumerable.Repeat(SpecialFolderCategory.System, 8), categories.Skip(8));
    }

    [Fact]
    public void Catalog_list_cannot_be_modified_through_a_cast()
    {
        Assert.False(SpecialFolderCatalog.All is IList<SpecialFolderInfo> { IsReadOnly: false });
    }
}
