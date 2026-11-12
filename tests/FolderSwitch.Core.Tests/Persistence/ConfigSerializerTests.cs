// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;

namespace FolderSwitch.Core.Tests.Persistence;

public class ConfigSerializerTests
{
    private const string DefaultColor = "#3b82f6";

    // ----- Round trip and format -----

    [Fact]
    public void Roundtrip_preserves_everything()
    {
        var original = TestConfigs.Rich();

        var copy = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(original));

        TestConfigs.AssertEquivalent(original, copy);
        // Spot checks of the values called out in the specification.
        Assert.Equal(330, copy.Tabs[0].Columns[0].Width);
        Assert.Equal(240, copy.Tabs[0].Columns[1].Width);
        var group = copy.Tabs[0].Columns[0].Groups[0];
        Assert.True(group.Collapsed);
        Assert.Equal("#f59e0b", group.Color);
        Assert.Equal(SpecialFolderKey.OneDrive, group.Items[0].Special);
        Assert.Null(group.Items[0].Path);
        Assert.Equal(@"\\nas\partage", group.Items[1].Path);
    }

    [Fact]
    public void Enums_are_written_as_camelCase_strings()
    {
        var json = ConfigSerializer.Serialize(TestConfigs.Rich());

        Assert.Contains("\"mode\": \"auto\"", json);
        Assert.Contains("\"kind\": \"folder\"", json);
        Assert.Contains("\"radius\": \"medium\"", json);
        Assert.Contains("\"special\": \"oneDrive\"", json);
    }

    [Fact]
    public void Serialize_is_indented_with_camelCase_property_names()
    {
        var json = ConfigSerializer.Serialize(TestConfigs.Minimal());

        Assert.Contains("\"schemaVersion\": 3", json);
        Assert.Contains("\"startWithWindows\"", json);
        Assert.DoesNotContain("\"SchemaVersion\"", json);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void Serialize_keeps_dictionary_keys_and_non_ascii_text_verbatim()
    {
        var json = ConfigSerializer.Serialize(TestConfigs.Rich());

        Assert.Contains("\"fluent:dark\"", json);
        Assert.Contains("Projets été", json);
    }

    [Fact]
    public void Hotkey_roundtrips_and_IsModifierOnly_is_not_written()
    {
        var config = TestConfigs.Minimal();
        config.Settings.Hotkey = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, 0x44);

        var json = ConfigSerializer.Serialize(config);
        var copy = ConfigSerializer.Deserialize(json);

        Assert.DoesNotContain("isModifierOnly", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Shift, 0x44), copy.Settings.Hotkey);
        Assert.False(copy.Settings.Hotkey.IsModifierOnly);
    }

    [Fact]
    public void Default_hotkey_roundtrips_as_modifier_only()
    {
        var copy = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(TestConfigs.Minimal()));

        Assert.Equal(Hotkey.Default, copy.Settings.Hotkey);
        Assert.True(copy.Settings.Hotkey.IsModifierOnly);
    }

    // ----- Icons -----

    [Fact]
    public void Icons_round_trip()
    {
        var original = TestConfigs.Rich();

        var json = ConfigSerializer.Serialize(original);
        var copy = ConfigSerializer.Deserialize(json);

        Assert.Contains("\"icon\": \"💼\"", json);
        Assert.Contains("\"iconChosen\": true", json);
        Assert.Contains("\"schemaVersion\": 3", json);
        TestConfigs.AssertEquivalent(original, copy);
        Assert.Equal("💼", copy.Tabs[0].Icon);
        Assert.True(copy.Tabs[0].IconChosen);
        var group = copy.Tabs[0].Columns[0].Groups[0];
        Assert.Equal("💵", group.Icon);
        Assert.False(group.IconChosen);
        Assert.Equal(3, copy.SchemaVersion);
    }

    [Fact]
    public void Config_without_icons_reads_as_automatic_empty()
    {
        var json = """
            { "schemaVersion": 2, "tabs": [ { "name": "A", "columns": [ { "groups": [
              { "name": "G", "color": "#3b82f6", "items": [] } ] } ] } ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        var tab = config.Tabs[0];
        Assert.Null(tab.Icon);
        Assert.False(tab.IconChosen);
        var group = tab.Columns[0].Groups[0];
        Assert.Null(group.Icon);
        Assert.False(group.IconChosen);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("💵💵💵")]
    public void Implausible_icon_is_dropped_and_choice_kept(string icon)
    {
        var json = """{ "schemaVersion": 2, "tabs": [ { "name": "A", "icon": "__ICON__", "iconChosen": true, "columns": [ { "groups": [ { "name": "G", "color": "#3b82f6", "icon": "__ICON__", "iconChosen": true } ] } ] } ] }"""
            .Replace("__ICON__", icon);

        var config = ConfigSerializer.Deserialize(json);

        var tab = config.Tabs[0];
        Assert.Null(tab.Icon);
        Assert.True(tab.IconChosen);
        var group = tab.Columns[0].Groups[0];
        Assert.Null(group.Icon);
        Assert.True(group.IconChosen);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "tabs": [ { "name": "A", "icon": 42 } ] }""")]
    [InlineData("""{ "schemaVersion": 2, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "icon": 42 } ] } ] } ] }""")]
    public void Icon_of_wrong_type_is_a_format_error(string json)
    {
        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(json));
    }

    // ----- Hard failures -----

    [Fact]
    public void Deserialize_rejects_newer_schema()
    {
        var json = """{ "schemaVersion": 4, "tabs": [ { "name": "A" } ] }""";

        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_rejects_missing_schema_version()
    {
        var json = """{ "tabs": [ { "name": "A" } ] }""";

        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("""{ "tabs": [""")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    public void Deserialize_rejects_invalid_json(string json)
    {
        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "tabs": [] }""")]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": null }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ null ] }""")]
    public void Deserialize_rejects_zero_tabs(string json)
    {
        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(json));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": "1", "tabs": [ { "name": "A" } ] }""")]
    [InlineData("""{ "schemaVersion": 1.5, "tabs": [ { "name": "A" } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "appearance": { "mode": "purple" }, "tabs": [ { "name": "A" } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "columns": [ { "width": "wide" } ] } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "id": "not-a-guid" } ] }""")]
    public void Deserialize_rejects_values_of_the_wrong_type(string json)
    {
        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_error_keeps_the_inner_exception_for_diagnostics()
    {
        var ex = Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize("""{ "tabs": ["""));

        Assert.NotNull(ex.InnerException);
    }

    // ----- Tolerant loading: content repaired, never refused -----

    [Theory]
    [InlineData("")]
    [InlineData("blue")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("3b82f6")]
    [InlineData("#gg82f6")]
    public void Group_with_empty_or_invalid_color_gets_the_default_color(string color)
    {
        var json = $$"""
            { "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "{{color}}" } ] } ] } ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(DefaultColor, config.Tabs[0].Columns[0].Groups[0].Color);
    }

    [Fact]
    public void Group_with_missing_or_null_color_gets_the_default_color()
    {
        var json = """
            { "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ { "groups": [
              { "name": "Missing" }, { "name": "Null", "color": null } ] } ] } ] }
            """;

        var groups = ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Groups;

        Assert.All(groups, group => Assert.Equal(DefaultColor, group.Color));
    }

    [Theory]
    [InlineData("#f59e0b")]
    [InlineData("#F59E0B")]
    public void Group_with_valid_color_keeps_it(string color)
    {
        var json = $$"""
            { "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "{{color}}" } ] } ] } ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(color, config.Tabs[0].Columns[0].Groups[0].Color);
    }

    [Fact]
    public void Item_with_neither_path_nor_special_is_dropped()
    {
        var json = """
            { "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "#22c55e", "items": [
              { "kind": "folder", "name": "Empty" },
              { "kind": "folder", "path": "" },
              { "kind": "folder", "path": "   " },
              { "kind": "folder", "path": "C:\\Work" },
              { "kind": "folder", "special": "desktop" },
              null
            ] } ] } ] } ] }
            """;

        var items = ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Groups[0].Items;

        Assert.Equal(2, items.Count);
        Assert.Equal(@"C:\Work", items[0].Path);
        Assert.Equal(SpecialFolderKey.Desktop, items[1].Special);
    }

    [Fact]
    public void Url_item_round_trips()
    {
        var original = TestConfigs.Rich();

        var json = ConfigSerializer.Serialize(original);
        var copy = ConfigSerializer.Deserialize(json);

        Assert.Contains("\"kind\": \"url\"", json);
        Assert.Contains("\"url\": \"https://www.morningstar.fr/fr/funds\"", json);
        Assert.Contains("\"schemaVersion\": 3", json);
        TestConfigs.AssertEquivalent(original, copy);
        var item = copy.Tabs[0].Columns[0].Groups[0].Items[3];
        Assert.Equal(ItemKind.Url, item.Kind);
        Assert.Equal("https://www.morningstar.fr/fr/funds", item.Url);
        Assert.Null(item.Path);
        Assert.Null(item.Special);
    }

    [Fact]
    public void Version_1_file_is_read()
    {
        var json = """
            { "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "#22c55e", "items": [
              { "kind": "folder", "path": "C:\\Work" }, { "kind": "file", "path": "C:\\Work\\a.txt" }
            ] } ] } ] } ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(2, config.Tabs[0].Columns[0].Groups[0].Items.Count);
        // Written back, the file carries the current version.
        Assert.Contains("\"schemaVersion\": 3", ConfigSerializer.Serialize(config));
    }

    [Fact]
    public void Version_2_file_is_read()
    {
        var json = """
            { "schemaVersion": 2, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "#22c55e", "items": [
              { "kind": "folder", "path": "C:\\Work" }, { "kind": "url", "url": "https://example.com/" }
            ] } ] } ] } ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(
            new[] { ItemKind.Folder, ItemKind.Url },
            config.Tabs[0].Columns[0].Groups[0].Items.Select(item => item.Kind));
        Assert.Contains("\"schemaVersion\": 3", ConfigSerializer.Serialize(config));
    }

    // ----- Separators -----

    [Fact]
    public void Separator_is_written_with_its_id_and_kind_only_and_read_back()
    {
        var original = TestConfigs.Minimal();
        var group = new Group { Name = "G", Color = DefaultColor };
        var separator = new Item { Kind = ItemKind.Separator };
        group.Items.Add(new Item { Kind = ItemKind.Folder, Path = @"C:\Work" });
        group.Items.Add(separator);
        original.Tabs[0].Columns[0].Groups.Add(group);

        var json = ConfigSerializer.Serialize(original);
        var copy = ConfigSerializer.Deserialize(json);

        using (var document = System.Text.Json.JsonDocument.Parse(json))
        {
            var written = document.RootElement.GetProperty("tabs")[0].GetProperty("columns")[0]
                .GetProperty("groups")[0].GetProperty("items")[1];
            Assert.Equal(new[] { "id", "kind" }, written.EnumerateObject().Select(property => property.Name));
            Assert.Equal("separator", written.GetProperty("kind").GetString());
        }

        // The other items keep every property, null ones included.
        Assert.Contains("\"url\": null", json);
        var items = copy.Tabs[0].Columns[0].Groups[0].Items;
        Assert.Equal(new[] { ItemKind.Folder, ItemKind.Separator }, items.Select(item => item.Kind));
        Assert.Equal(separator.Id, items[1].Id);
    }

    [Fact]
    public void Leading_trailing_and_consecutive_separators_are_kept()
    {
        var json = """
            { "schemaVersion": 3, "tabs": [ { "name": "A", "columns": [ { "groups": [
              { "name": "G", "color": "#22c55e", "items": [
                { "kind": "separator" }, { "kind": "folder", "path": "C:\\Work" }, { "kind": "separator" }, { "kind": "separator" } ] },
              { "name": "Only lines", "color": "#22c55e", "items": [ { "kind": "separator" } ] }
            ] } ] } ] }
            """;

        var groups = ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Groups;

        Assert.Equal(
            new[] { ItemKind.Separator, ItemKind.Folder, ItemKind.Separator, ItemKind.Separator },
            groups[0].Items.Select(item => item.Kind));
        Assert.Equal(ItemKind.Separator, Assert.Single(groups[1].Items).Kind);
    }

    [Fact]
    public void Name_path_special_and_url_of_a_separator_are_dropped()
    {
        var json = """
            { "schemaVersion": 3, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "#22c55e", "items": [
              { "kind": "separator", "name": "Line", "path": "C:\\Work", "special": "desktop", "url": "https://example.com/" }
            ] } ] } ] } ] }
            """;

        var separator = Assert.Single(ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Groups[0].Items);

        Assert.Equal(ItemKind.Separator, separator.Kind);
        Assert.Null(separator.Name);
        Assert.Null(separator.Path);
        Assert.Null(separator.Special);
        Assert.Null(separator.Url);
    }

    [Fact]
    public void Current_schema_version_is_the_default_of_a_new_configuration() =>
        Assert.Equal(ConfigSerializer.CurrentSchemaVersion, new AppConfig().SchemaVersion);

    [Theory]
    [InlineData("file:///C:/wiki/index.html")]
    [InlineData("file://server/share/index.html")]
    [InlineData("ftp://example.com/pub")]
    public void File_and_ftp_url_items_are_kept(string address)
    {
        var json = $$"""
            { "schemaVersion": 2, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "#22c55e", "items": [
              { "kind": "url", "url": "{{address}}" }
            ] } ] } ] } ] }
            """;

        var items = ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Groups[0].Items;

        Assert.Equal(address, Assert.Single(items).Url);
    }

    [Theory]
    [InlineData("""{ "kind": "url" }""")]
    [InlineData("""{ "kind": "url", "url": "javascript:x" }""")]
    [InlineData("""{ "kind": "url", "url": "gopher://x" }""")]
    [InlineData("""{ "kind": "url", "url": "ftp://user:pw@x/" }""")]
    [InlineData("""{ "kind": "url", "url": "file:C:/x.html" }""")]
    [InlineData("""{ "kind": "url", "url": "C:/x.html" }""")]
    [InlineData("""{ "kind": "url", "url": "example.com" }""")]
    [InlineData("""{ "kind": "url", "url": "   " }""")]
    public void Url_item_without_valid_web_address_is_removed(string item)
    {
        var json = $$"""
            { "schemaVersion": 2, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "#22c55e", "items": [
              {{item}}, { "kind": "url", "url": "https://example.com/" }
            ] } ] } ] } ] }
            """;

        var items = ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Groups[0].Items;

        Assert.Equal("https://example.com/", Assert.Single(items).Url);
    }

    // ----- Column width -----

    private static string OneColumnJson(string appearance, string column) =>
        "{ \"schemaVersion\": 3, " + appearance + "\"tabs\": [ { \"name\": \"A\", \"columns\": [ " + column + " ] } ] }";

    [Fact]
    public void Column_without_width_gets_default_width()
    {
        var config = ConfigSerializer.Deserialize(OneColumnJson("\"appearance\": { }, ", "{ \"groups\": [] }"));

        Assert.Equal(220, config.Tabs[0].Columns[0].Width);
        Assert.Equal(220, config.Appearance.DefaultColumnWidth);
    }

    [Fact]
    public void Column_with_null_width_gets_appearance_default()
    {
        var json = OneColumnJson("\"appearance\": { \"defaultColumnWidth\": 300 }, ", "{ \"width\": null, \"groups\": [] }");

        Assert.Equal(300, ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Width);
    }

    [Fact]
    public void Manual_width_is_kept()
    {
        var json = OneColumnJson("\"appearance\": { \"defaultColumnWidth\": 300 }, ", "{ \"width\": 340, \"groups\": [] }");

        Assert.Equal(340, ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Width);
    }

    [Theory]
    [InlineData(0, 150)]
    [InlineData(-5, 150)]
    [InlineData(9999, 520)]
    [InlineData(223, 220)]
    [InlineData(225, 230)]
    public void Default_column_width_is_clamped_and_rounded(int stored, int expected)
    {
        var json = OneColumnJson("\"appearance\": { \"defaultColumnWidth\": " + stored + " }, ", "{ \"groups\": [] }");

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(expected, config.Appearance.DefaultColumnWidth);
        Assert.Equal(expected, config.Tabs[0].Columns[0].Width);
    }

    [Fact]
    public void Tab_without_column_gets_one_at_default_width()
    {
        var json = "{ \"schemaVersion\": 3, \"tabs\": [ { \"name\": \"A\", \"columns\": [] } ] }";

        Assert.Equal(220, Assert.Single(ConfigSerializer.Deserialize(json).Tabs[0].Columns).Width);
    }

    [Fact]
    public void Default_column_width_round_trips()
    {
        var original = TestConfigs.Minimal();
        original.Appearance.DefaultColumnWidth = 310;

        var json = ConfigSerializer.Serialize(original);

        Assert.Contains("\"defaultColumnWidth\"", json);
        Assert.Contains("\"schemaVersion\": 3", json);
        Assert.Equal(310, ConfigSerializer.Deserialize(json).Appearance.DefaultColumnWidth);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [] } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ null ] } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "name": "A" } ] }""")]
    public void Tab_without_a_column_gets_an_empty_one(string json)
    {
        var column = Assert.Single(ConfigSerializer.Deserialize(json).Tabs[0].Columns);

        Assert.Empty(column.Groups);
        Assert.Equal(220, column.Width);
    }

    [Fact]
    public void Null_lists_become_empty_and_null_names_become_empty_strings()
    {
        var json = """
            { "schemaVersion": 1, "tabs": [
              { "name": null, "columns": null },
              { "name": "B", "columns": [ { "groups": null }, { "groups": [ { "name": null, "color": "#22c55e", "items": null } ] } ] }
            ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(string.Empty, config.Tabs[0].Name);
        Assert.Empty(Assert.Single(config.Tabs[0].Columns).Groups);
        Assert.Empty(config.Tabs[1].Columns[0].Groups);
        var group = Assert.Single(config.Tabs[1].Columns[1].Groups);
        Assert.Equal(string.Empty, group.Name);
        Assert.Empty(group.Items);
    }

    [Fact]
    public void Item_name_stays_null_because_null_means_default_name()
    {
        var json = """
            { "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ { "groups": [ { "name": "G", "color": "#22c55e", "items": [
              { "kind": "folder", "path": "C:\\Work", "name": null } ] } ] } ] } ] }
            """;

        var item = ConfigSerializer.Deserialize(json).Tabs[0].Columns[0].Groups[0].Items[0];

        Assert.Null(item.Name);
    }

    [Fact]
    public void Null_elements_in_lists_are_dropped()
    {
        var json = """
            { "schemaVersion": 1, "tabs": [ null, { "name": "A", "columns": [ null, { "groups": [ null, { "name": "G", "color": "#22c55e" } ] } ] } ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        var tab = Assert.Single(config.Tabs);
        var column = Assert.Single(tab.Columns);
        Assert.Single(column.Groups);
    }

    [Fact]
    public void Null_sections_fall_back_to_defaults()
    {
        var json = """{ "schemaVersion": 1, "settings": null, "appearance": null, "tabs": [ { "name": "A" } ] }""";

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(Hotkey.Default, config.Settings.Hotkey);
        Assert.True(config.Settings.StartWithWindows);
        Assert.Equal(ThemeMode.Light, config.Appearance.Mode);
        Assert.Empty(config.Appearance.Backgrounds);
    }

    [Fact]
    public void Null_hotkey_and_null_backgrounds_fall_back_to_defaults()
    {
        var json = """
            { "schemaVersion": 1, "settings": { "hotkey": null }, "appearance": { "backgrounds": null }, "tabs": [ { "name": "A" } ] }
            """;

        var config = ConfigSerializer.Deserialize(json);

        Assert.Equal(Hotkey.Default, config.Settings.Hotkey);
        Assert.Empty(config.Appearance.Backgrounds);
    }

    // ----- Automatic update check -----

    [Fact]
    public void Automatic_update_check_is_on_when_the_setting_is_missing()
    {
        // A file written before the setting existed.
        var json = """{ "schemaVersion": 2, "settings": { "startWithWindows": false }, "tabs": [ { "name": "A" } ] }""";

        var config = ConfigSerializer.Deserialize(json);

        Assert.True(config.Settings.CheckForUpdatesAutomatically);
    }

    [Fact]
    public void Automatic_update_check_off_is_read_back()
    {
        var original = TestConfigs.Minimal();
        original.Settings.CheckForUpdatesAutomatically = false;

        var json = ConfigSerializer.Serialize(original);
        var copy = ConfigSerializer.Deserialize(json);

        Assert.Contains("\"checkForUpdatesAutomatically\": false", json);
        Assert.False(copy.Settings.CheckForUpdatesAutomatically);
        Assert.Equal(3, copy.SchemaVersion);
    }

    [Fact]
    public void Automatic_update_check_of_the_wrong_type_is_a_format_error()
    {
        var json = """{ "schemaVersion": 2, "settings": { "checkForUpdatesAutomatically": "oui" }, "tabs": [ { "name": "A" } ] }""";

        Assert.Throws<ConfigFormatException>(() => ConfigSerializer.Deserialize(json));
    }

    [Fact]
    public void Missing_ids_are_generated()
    {
        var json = """{ "schemaVersion": 1, "tabs": [ { "name": "A" }, { "name": "B" } ] }""";

        var config = ConfigSerializer.Deserialize(json);

        Assert.NotEqual(Guid.Empty, config.Tabs[0].Id);
        Assert.NotEqual(config.Tabs[0].Id, config.Tabs[1].Id);
    }
}
