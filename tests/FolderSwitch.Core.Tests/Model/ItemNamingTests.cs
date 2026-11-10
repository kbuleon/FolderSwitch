// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Tests.Model;

public class ItemNamingTests
{
    private static string Localize(string key) => "T:" + key;

    [Theory]
    // An explicit name always wins.
    [InlineData("Mes clients", @"D:\Clients\Acme", null, "Mes clients")]
    [InlineData("Mon PC", null, SpecialFolderKey.ThisPC, "Mon PC")]
    // A blank name means "default name".
    [InlineData(null, @"D:\Clients\Acme", null, "Acme")]
    [InlineData("", @"D:\Clients\Acme", null, "Acme")]
    [InlineData("   ", @"D:\Clients\Acme", null, "Acme")]
    [InlineData("   ", null, SpecialFolderKey.Desktop, "T:SpecialFolder.Desktop")]
    // Special folders use the translated catalog name.
    [InlineData(null, null, SpecialFolderKey.ThisPC, "T:SpecialFolder.ThisPC")]
    [InlineData(null, null, SpecialFolderKey.RecycleBin, "T:SpecialFolder.RecycleBin")]
    [InlineData(null, null, SpecialFolderKey.RoamingAppData, "T:SpecialFolder.RoamingAppData")]
    // Otherwise the last segment of the path.
    [InlineData(null, @"D:\Clients\Acme\", null, "Acme")]
    [InlineData(null, "D:/Clients/Acme/", null, "Acme")]
    [InlineData(null, @"D:\Clients\Acme\\", null, "Acme")]
    [InlineData(null, @"C:\Docs\notes.txt", null, "notes.txt")]
    [InlineData(null, @"\\nas\partage", null, "partage")]
    [InlineData(null, @"\\nas\partage\", null, "partage")]
    [InlineData(null, @"\\nas\partage\Photos", null, "Photos")]
    [InlineData(null, "Clients", null, "Clients")]
    // A drive root keeps its separator.
    [InlineData(null, @"D:\", null, @"D:\")]
    [InlineData(null, "d:/", null, "d:/")]
    [InlineData(null, "D:", null, "D:")]
    // Nothing to show.
    [InlineData(null, null, null, "")]
    [InlineData(null, "", null, "")]
    [InlineData(null, "   ", null, "")]
    public void DisplayName_follows_the_naming_rules(string? name, string? path, SpecialFolderKey? special, string expected)
    {
        var item = new Item { Kind = ItemKind.Folder, Name = name, Path = path, Special = special };

        Assert.Equal(expected, ItemNaming.DisplayName(item, Localize));
    }

    [Theory]
    [InlineData("ftp://www.a.fr/pub", "a.fr")]
    [InlineData("file:///C:/wiki/index.html", "index.html")]
    [InlineData("file://server/share/My%20Page.html", "My Page.html")]
    [InlineData("file://server/", "server")]
    public void Ftp_and_file_items_without_name_are_named_by_host_or_file(string url, string expected)
    {
        var item = new Item { Kind = ItemKind.Url, Url = url };

        Assert.Equal(expected, ItemNaming.DisplayName(item, Localize));
    }

    [Fact]
    public void Url_item_without_name_is_named_by_host()
    {
        var item = new Item { Kind = ItemKind.Url, Url = "https://www.morningstar.fr/x" };

        Assert.Equal("morningstar.fr", ItemNaming.DisplayName(item, Localize));

        item.Name = "Morningstar";

        Assert.Equal("Morningstar", ItemNaming.DisplayName(item, Localize));
    }

    [Fact]
    public void DisplayName_does_not_localize_when_a_name_or_a_path_is_enough()
    {
        static string Fail(string key) => throw new InvalidOperationException("localize must not be called: " + key);

        Assert.Equal("Perso", ItemNaming.DisplayName(new Item { Name = "Perso", Special = SpecialFolderKey.Desktop }, Fail));
        Assert.Equal("Acme", ItemNaming.DisplayName(new Item { Path = @"D:\Clients\Acme" }, Fail));
    }

    [Fact]
    public void DisplayName_prefers_the_special_folder_over_a_stray_path()
    {
        var item = new Item { Path = @"C:\Users\Karl\Desktop", Special = SpecialFolderKey.Desktop };

        Assert.Equal("T:SpecialFolder.Desktop", ItemNaming.DisplayName(item, Localize));
    }

    [Fact]
    public void DisplayName_passes_the_catalog_name_key_to_the_localizer()
    {
        string? received = null;

        var result = ItemNaming.DisplayName(
            new Item { Special = SpecialFolderKey.ThisPC },
            key =>
            {
                received = key;
                return "Ce PC";
            });

        Assert.Equal("SpecialFolder.ThisPC", received);
        Assert.Equal("Ce PC", result);
    }

    [Fact]
    public void DisplayName_of_a_separator_is_empty_whatever_it_carries()
    {
        var called = false;
        var item = new Item { Kind = ItemKind.Separator, Name = "Line", Path = @"D:\X", Special = SpecialFolderKey.Desktop };

        var result = ItemNaming.DisplayName(item, key =>
        {
            called = true;
            return key;
        });

        Assert.Equal(string.Empty, result);
        Assert.False(called);
    }

    [Fact]
    public void DisplayName_does_not_throw_on_an_undefined_special_folder_value()
    {
        var item = new Item { Special = (SpecialFolderKey)999 };

        Assert.Equal("T:SpecialFolder.999", ItemNaming.DisplayName(item, Localize));
    }
}
