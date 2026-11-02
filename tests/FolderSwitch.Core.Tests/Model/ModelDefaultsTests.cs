// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Tests.Model;

public class ModelDefaultsTests
{
    [Fact]
    public void Hotkey_default_is_ctrl_plus_win_modifier_only()
    {
        var hotkey = Hotkey.Default;

        Assert.Equal(KeyModifiers.Ctrl | KeyModifiers.Win, hotkey.Modifiers);
        Assert.Null(hotkey.VirtualKey);
        Assert.True(hotkey.IsModifierOnly);
        Assert.False(new Hotkey(KeyModifiers.Ctrl, 0x44).IsModifierOnly);
    }

    [Fact]
    public void AppConfig_defaults_match_the_specification()
    {
        var config = new AppConfig();

        Assert.Equal(2, config.SchemaVersion);
        Assert.Empty(config.Tabs);
        Assert.Equal(Hotkey.Default, config.Settings.Hotkey);
        Assert.True(config.Settings.StartWithWindows);
        Assert.False(config.Settings.FirstRunCompleted);
        Assert.Null(config.Settings.Language);
        Assert.Null(config.Settings.ActiveTabId);
        Assert.Equal(ThemeMode.Light, config.Appearance.Mode);
        Assert.Equal("fluent", config.Appearance.Theme);
        Assert.Empty(config.Appearance.Backgrounds);
        Assert.Equal(GroupRadius.Theme, config.Appearance.Radius);
    }

    [Fact]
    public void New_model_objects_get_distinct_ids_and_empty_collections()
    {
        var first = new Item();
        var second = new Item();

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(new Group().Id, new Group().Id);
        Assert.NotEqual(new Column().Id, new Column().Id);
        Assert.NotEqual(new Tab().Id, new Tab().Id);
        Assert.Empty(new Group().Items);
        Assert.Empty(new Column().Groups);
        Assert.Empty(new Tab().Columns);
        Assert.Null(new Column().Width);
    }
}
