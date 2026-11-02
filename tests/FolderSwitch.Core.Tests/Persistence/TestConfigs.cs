// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Editing;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Tests.Persistence;

internal static class TestConfigs
{
    /// <summary>A valid configuration with a single, empty tab.</summary>
    public static AppConfig Minimal(string tabName = "Main")
    {
        var config = new AppConfig();
        BoardEditor.AddTab(config, tabName);
        return config;
    }

    /// <summary>A configuration that exercises every field of the model.</summary>
    public static AppConfig Rich()
    {
        var work = new Tab
        {
            Name = "Travail",
            Columns =
            {
                new Column
                {
                    Width = 330,
                    Groups =
                    {
                        new Group
                        {
                            Name = "Projets été",
                            Color = "#f59e0b",
                            Collapsed = true,
                            Items =
                            {
                                new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.OneDrive },
                                new Item { Kind = ItemKind.Folder, Path = @"\\nas\partage", Name = "Partage NAS" },
                                new Item { Kind = ItemKind.File, Path = @"C:\Docs\notes.txt" },
                                new Item { Kind = ItemKind.Url, Url = "https://www.morningstar.fr/fr/funds", Name = "Morningstar" },
                            },
                        },
                    },
                },
                new Column { Width = null },
            },
        };
        var home = new Tab
        {
            Name = "Maison",
            Columns = { new Column { Width = 520, Groups = { new Group { Name = "Vide", Color = "#22c55e" } } } },
        };

        return new AppConfig
        {
            Settings = new Settings
            {
                Language = "fr",
                Hotkey = new Hotkey(KeyModifiers.Ctrl | KeyModifiers.Alt, 0x4B),
                StartWithWindows = false,
                FirstRunCompleted = true,
                ActiveTabId = home.Id,
            },
            Appearance = new Appearance
            {
                Mode = ThemeMode.Auto,
                Theme = "fluent",
                Backgrounds = { ["fluent:light"] = "#ffffff", ["fluent:dark"] = "#1e1e1e" },
                Radius = GroupRadius.Medium,
            },
            Tabs = { work, home },
        };
    }

    /// <summary>Asserts that two configurations are structurally equal, field by field.</summary>
    public static void AssertEquivalent(AppConfig expected, AppConfig actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);

        Assert.Equal(expected.Settings.Language, actual.Settings.Language);
        Assert.Equal(expected.Settings.Hotkey, actual.Settings.Hotkey);
        Assert.Equal(expected.Settings.StartWithWindows, actual.Settings.StartWithWindows);
        Assert.Equal(expected.Settings.FirstRunCompleted, actual.Settings.FirstRunCompleted);
        Assert.Equal(expected.Settings.ActiveTabId, actual.Settings.ActiveTabId);

        Assert.Equal(expected.Appearance.Mode, actual.Appearance.Mode);
        Assert.Equal(expected.Appearance.Theme, actual.Appearance.Theme);
        Assert.Equal(expected.Appearance.Backgrounds, actual.Appearance.Backgrounds);
        Assert.Equal(expected.Appearance.Radius, actual.Appearance.Radius);

        Assert.Equal(expected.Tabs.Count, actual.Tabs.Count);
        for (var t = 0; t < expected.Tabs.Count; t++)
        {
            var et = expected.Tabs[t];
            var at = actual.Tabs[t];
            Assert.Equal(et.Id, at.Id);
            Assert.Equal(et.Name, at.Name);
            Assert.Equal(et.Columns.Count, at.Columns.Count);
            for (var c = 0; c < et.Columns.Count; c++)
            {
                var ec = et.Columns[c];
                var ac = at.Columns[c];
                Assert.Equal(ec.Id, ac.Id);
                Assert.Equal(ec.Width, ac.Width);
                Assert.Equal(ec.Groups.Count, ac.Groups.Count);
                for (var g = 0; g < ec.Groups.Count; g++)
                {
                    var eg = ec.Groups[g];
                    var ag = ac.Groups[g];
                    Assert.Equal(eg.Id, ag.Id);
                    Assert.Equal(eg.Name, ag.Name);
                    Assert.Equal(eg.Color, ag.Color);
                    Assert.Equal(eg.Collapsed, ag.Collapsed);
                    Assert.Equal(eg.Items.Count, ag.Items.Count);
                    for (var i = 0; i < eg.Items.Count; i++)
                    {
                        var ei = eg.Items[i];
                        var ai = ag.Items[i];
                        Assert.Equal(ei.Id, ai.Id);
                        Assert.Equal(ei.Kind, ai.Kind);
                        Assert.Equal(ei.Name, ai.Name);
                        Assert.Equal(ei.Path, ai.Path);
                        Assert.Equal(ei.Special, ai.Special);
                        Assert.Equal(ei.Url, ai.Url);
                    }
                }
            }
        }
    }
}
