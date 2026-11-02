// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Editing;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Persistence;

/// <summary>Builds the configuration a first run starts from.</summary>
public static class DefaultConfigs
{
    /// <summary>The palette's blue.</summary>
    private const string SampleGroupColor = "#3b82f6";

    private static readonly SpecialFolderKey[] SampleFolders =
    [
        SpecialFolderKey.Desktop,
        SpecialFolderKey.Documents,
        SpecialFolderKey.Downloads,
        SpecialFolderKey.Pictures,
    ];

    /// <summary>
    /// Creates a complete configuration (default settings and appearance) with exactly one tab, which
    /// becomes the active tab. The names are given already translated: they are user content from then on.
    /// </summary>
    /// <param name="start">What the tab contains.</param>
    /// <param name="tabName">Name of the tab.</param>
    /// <param name="groupName">Name of the sample group; ignored for <see cref="StartContent.EmptyTab"/>.</param>
    /// <param name="suggestIcon">
    /// Gives the icon suggested for a name, or <see langword="null"/> for none. The suggestions are automatic
    /// (<c>IconChosen</c> stays false), so a rename may replace them. Omitted: no icon.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is not a defined value.</exception>
    public static AppConfig Create(StartContent start, string tabName, string groupName, Func<string, string?>? suggestIcon = null)
    {
        if (!Enum.IsDefined(start))
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Unknown start content.");
        }

        var config = new AppConfig();
        var tab = BoardEditor.AddTab(config, tabName);
        tab.Icon = Suggest(suggestIcon, tabName);

        if (start == StartContent.SampleTab)
        {
            var group = BoardEditor.AddGroup(tab.Columns[0], groupName, SampleGroupColor);
            group.Icon = Suggest(suggestIcon, groupName);
            foreach (var key in SampleFolders)
            {
                BoardEditor.AddItem(group, new Item { Kind = ItemKind.Folder, Special = key });
            }
        }

        config.Settings.ActiveTabId = tab.Id;
        return config;
    }

    private static string? Suggest(Func<string, string?>? suggestIcon, string name)
    {
        var icon = suggestIcon?.Invoke(name);
        return IconText.IsPlausible(icon) ? icon : null;
    }
}
