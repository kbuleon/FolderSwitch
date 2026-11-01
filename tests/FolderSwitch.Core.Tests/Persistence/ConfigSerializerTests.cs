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
        Assert.Null(copy.Tabs[0].Columns[1].Width);
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

        Assert.Contains("\"schemaVersion\": 2", json);
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

    // ----- Hard failures -----

    [Fact]
    public void Deserialize_rejects_newer_schema()
    {
        var json = """{ "schemaVersion": 3, "tabs": [ { "name": "A" } ] }""";

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
        Assert.Contains("\"schemaVersion\": 2", json);
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
        Assert.Contains("\"schemaVersion\": 2", ConfigSerializer.Serialize(config));
    }

    [Fact]
    public void Current_schema_version_is_the_default_of_a_new_configuration() =>
        Assert.Equal(ConfigSerializer.CurrentSchemaVersion, new AppConfig().SchemaVersion);

    [Theory]
    [InlineData("""{ "kind": "url" }""")]
    [InlineData("""{ "kind": "url", "url": "javascript:x" }""")]
    [InlineData("""{ "kind": "url", "url": "ftp://x" }""")]
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

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [] } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "name": "A", "columns": [ null ] } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "tabs": [ { "name": "A" } ] }""")]
    public void Tab_without_a_column_gets_an_empty_one(string json)
    {
        var column = Assert.Single(ConfigSerializer.Deserialize(json).Tabs[0].Columns);

        Assert.Empty(column.Groups);
        Assert.Null(column.Width);
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

    [Fact]
    public void Missing_ids_are_generated()
    {
        var json = """{ "schemaVersion": 1, "tabs": [ { "name": "A" }, { "name": "B" } ] }""";

        var config = ConfigSerializer.Deserialize(json);

        Assert.NotEqual(Guid.Empty, config.Tabs[0].Id);
        Assert.NotEqual(config.Tabs[0].Id, config.Tabs[1].Id);
    }
}
