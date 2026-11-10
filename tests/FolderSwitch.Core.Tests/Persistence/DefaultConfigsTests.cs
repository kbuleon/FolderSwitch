// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;

namespace FolderSwitch.Core.Tests.Persistence;

public class DefaultConfigsTests
{
    [Fact]
    public void Sample_tab_has_windows_group_with_four_special_folders()
    {
        var config = DefaultConfigs.Create(StartContent.SampleTab, "Principal", "Windows");

        var tab = Assert.Single(config.Tabs);
        Assert.Equal("Principal", tab.Name);
        Assert.Equal(2, tab.Columns.Count);

        var group = Assert.Single(tab.Columns[0].Groups);
        Assert.Equal("Windows", group.Name);
        Assert.Equal("#3b82f6", group.Color);
        Assert.False(group.Collapsed);

        Assert.Equal(
            new SpecialFolderKey?[]
            {
                SpecialFolderKey.Desktop,
                SpecialFolderKey.Documents,
                SpecialFolderKey.Downloads,
                SpecialFolderKey.Pictures,
            },
            group.Items.Select(item => item.Special));
        Assert.All(group.Items, item =>
        {
            Assert.Equal(ItemKind.Folder, item.Kind);
            Assert.Null(item.Name);
            Assert.Null(item.Path);
        });
        Assert.Equal(4, group.Items.Select(item => item.Id).Distinct().Count());

        Assert.Empty(tab.Columns[1].Groups);
    }

    [Fact]
    public void Sample_gets_suggested_icons()
    {
        var config = DefaultConfigs.Create(
            StartContent.SampleTab, "Principal", "Windows", name => name == "Windows" ? "🪟" : null);

        var tab = Assert.Single(config.Tabs);
        Assert.Null(tab.Icon);
        Assert.False(tab.IconChosen);
        var group = Assert.Single(tab.Columns[0].Groups);
        Assert.Equal("🪟", group.Icon);
        Assert.False(group.IconChosen);
    }

    [Fact]
    public void Sample_has_no_icon_without_a_suggestion()
    {
        var config = DefaultConfigs.Create(StartContent.SampleTab, "Principal", "Windows");

        var tab = Assert.Single(config.Tabs);
        Assert.Null(tab.Icon);
        Assert.False(tab.IconChosen);
        var group = Assert.Single(tab.Columns[0].Groups);
        Assert.Null(group.Icon);
        Assert.False(group.IconChosen);
    }

    [Fact]
    public void Empty_tab_has_two_empty_columns()
    {
        var config = DefaultConfigs.Create(StartContent.EmptyTab, "Principal", "Windows");

        var tab = Assert.Single(config.Tabs);
        Assert.Equal("Principal", tab.Name);
        Assert.Equal(2, tab.Columns.Count);
        Assert.All(tab.Columns, column =>
        {
            Assert.Empty(column.Groups);
            Assert.Null(column.Width);
        });
    }

    [Theory]
    [InlineData(StartContent.SampleTab)]
    [InlineData(StartContent.EmptyTab)]
    public void Active_tab_is_the_created_tab(StartContent start)
    {
        var config = DefaultConfigs.Create(start, "Principal", "Windows");

        Assert.Equal(config.Tabs[0].Id, config.Settings.ActiveTabId);
    }

    [Theory]
    [InlineData(StartContent.SampleTab)]
    [InlineData(StartContent.EmptyTab)]
    public void Settings_and_appearance_keep_their_defaults(StartContent start)
    {
        var config = DefaultConfigs.Create(start, "Principal", "Windows");
        var settings = new Settings();
        var appearance = new Appearance();

        Assert.Equal(3, config.SchemaVersion);
        Assert.Equal(settings.Language, config.Settings.Language);
        Assert.Equal(settings.Hotkey, config.Settings.Hotkey);
        Assert.Equal(settings.StartWithWindows, config.Settings.StartWithWindows);
        Assert.Equal(settings.FirstRunCompleted, config.Settings.FirstRunCompleted);
        Assert.Equal(appearance.Mode, config.Appearance.Mode);
        Assert.Equal(appearance.Theme, config.Appearance.Theme);
        Assert.Equal(appearance.Radius, config.Appearance.Radius);
        Assert.Empty(config.Appearance.Backgrounds);
    }

    [Theory]
    [InlineData(StartContent.SampleTab)]
    [InlineData(StartContent.EmptyTab)]
    public void Created_config_survives_a_serializer_round_trip(StartContent start)
    {
        var config = DefaultConfigs.Create(start, "Principal", "Windows");

        var roundTripped = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));

        TestConfigs.AssertEquivalent(config, roundTripped);
    }

    [Fact]
    public void Each_call_returns_a_new_config_with_new_ids()
    {
        var first = DefaultConfigs.Create(StartContent.SampleTab, "Principal", "Windows");
        var second = DefaultConfigs.Create(StartContent.SampleTab, "Principal", "Windows");

        Assert.NotSame(first, second);
        Assert.NotEqual(first.Tabs[0].Id, second.Tabs[0].Id);
        Assert.NotEqual(first.Tabs[0].Columns[0].Groups[0].Id, second.Tabs[0].Columns[0].Groups[0].Id);
    }

    [Fact]
    public void Unknown_start_content_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DefaultConfigs.Create((StartContent)99, "Principal", "Windows"));
    }
}
