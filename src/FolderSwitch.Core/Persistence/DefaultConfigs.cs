using FolderSwitch.Core.Editing;
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is not a defined value.</exception>
    public static AppConfig Create(StartContent start, string tabName, string groupName)
    {
        if (!Enum.IsDefined(start))
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Unknown start content.");
        }

        var config = new AppConfig();
        var tab = BoardEditor.AddTab(config, tabName);

        if (start == StartContent.SampleTab)
        {
            var group = BoardEditor.AddGroup(tab.Columns[0], groupName, SampleGroupColor);
            foreach (var key in SampleFolders)
            {
                BoardEditor.AddItem(group, new Item { Kind = ItemKind.Folder, Special = key });
            }
        }

        config.Settings.ActiveTabId = tab.Id;
        return config;
    }
}
