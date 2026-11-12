// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;

namespace FolderSwitch.App.Diagnostics.Snapshots;

/// <summary>
/// The configuration the snapshots are drawn with: the board of the mockups (first tab, three columns: Projects,
/// Clients / Personal, Archive collapsed, Finance / Tools, Network collapsed; then the home and the admin tabs).
/// Finance holds one item of each kind: a folder, two files of types Windows has icons for, a site whose favicon is in
/// the snapshot's cache (<see cref="FaviconHost"/>) and one without, the files and the sites set apart by a separator.
/// The tabs and groups have icons, except Clients (beside Projects, which has one) and a few of the twelve tabs.
/// Names, icons, paths and addresses are examples: they are the user's content, so they follow the snapshot's
/// language (<see cref="SampleNames"/>), French being the reference of the mockups.
/// </summary>
internal static class SnapshotSample
{
    private const string Blue = "#3b82f6";
    private const string Cyan = "#06b6d4";
    private const string Green = "#22c55e";
    private const string Amber = "#f59e0b";
    private const string Orange = "#f97316";
    private const string Pink = "#ec4899";
    private const string Violet = "#8b5cf6";
    private const string Slate = "#64748b";
    private const string Red = "#ef4444";

    /// <summary>The host of the site whose favicon the snapshots keep (see <see cref="SnapshotServices.FaviconFile"/>).</summary>
    public const string FaviconHost = "www.morningstar.fr";

    /// <summary>The mockups' board, on the first tab, every column at the default width.</summary>
    public static AppConfig Create(string language)
    {
        var n = SampleNames.For(language);
        var work = Tab(n.TabWork, "💼",
            Column(
                Group(n.Projects, "📁", Blue, false,
                    Folder(@"C:\Claude Projects\FolderSwitch"),
                    Folder($@"D:\{n.ClientsDir}\Acme\{n.WebsiteDir}", n.AcmeSite),
                    Folder($@"D:\{n.DesignDir}\{n.Mockups}"),
                    File($@"D:\{n.ClientsDir}\Acme\{n.ProjectBrief}")),
                Group(n.Clients, Cyan, false,
                    Folder(n.AcmePath),
                    Folder($@"D:\{n.ClientsDir}\{n.CompanyDir}", n.Company),
                    Folder($@"D:\{n.AccountingDir}\{n.InvoicesDir}6", n.Invoices))),
            Column(
                Group(n.Personal, "👤", Amber, false,
                    Special(SpecialFolderKey.Downloads),
                    Special(SpecialFolderKey.Documents),
                    Special(SpecialFolderKey.Desktop),
                    File($@"%USERPROFILE%\Documents\{n.MeetingNotes}")),
                Group(n.Archive, "🗄️", Slate, true,
                    Folder($@"E:\{n.ArchiveDir}4"),
                    Folder($@"E:\{n.ArchiveDir}5")),
                Group(n.Finance, "💵", Red, false,
                    Folder($@"D:\{n.Finance}\{n.Statements}"),
                    File($@"D:\{n.Finance}\{n.Budget}"),
                    File($@"D:\{n.Finance}\{n.TaxNotice}"),
                    Separator(),
                    Site("https://" + FaviconHost + "/fr/", "Morningstar"),
                    Site(n.SecondSite))),
            Column(
                Group(n.Tools, "🧰", Violet, false,
                    Folder($@"C:\{n.ToolsDir}\Scripts"),
                    Folder($@"C:\ProgramData\{n.AppDir}\logs", n.Logs),
                    File(@"C:\Windows\System32\drivers\etc\hosts")),
                Group(n.Network, "🌐", Green, true,
                    Folder($@"\\nas\{n.ShareDir}", n.NasShare),
                    Folder($@"\\nas\{n.Photos.ToLowerInvariant()}", n.Photos))));

        var home = Tab(n.TabHome, "🏠",
            Column(Group(n.Papers, "🧾", Orange, false,
                Folder($@"D:\{n.PersonalDir}\{n.HomeDir}\{n.InvoicesDir}", n.HomeBills),
                Folder($@"D:\{n.PersonalDir}\{n.Insurance}"),
                File($@"D:\{n.PersonalDir}\{n.HomeDir}\{n.RenovationPlan}"))),
            Column(Group(n.Photos, "📷", Pink, false,
                Folder($@"D:\{n.Photos}5"),
                Folder($@"D:\{n.Photos}6"),
                Folder($@"D:\{n.Photos}\{n.PhotoAlbums}"))),
            Column(Group(n.Leisure, "🎮", Green, true,
                Special(SpecialFolderKey.Music),
                Folder(@"%USERPROFILE%\Saved Games", n.SavedGames))));

        var admin = Tab(n.TabAdmin, "⚙️",
            Column(Group(n.System, "💻", Slate, false,
                Special(SpecialFolderKey.ProgramFiles),
                Special(SpecialFolderKey.RoamingAppData),
                File(@"C:\Windows\System32\drivers\etc\hosts"))),
            Column(Group(n.Shares, "🌐", Cyan, false,
                Folder($@"\\nas\{n.ShareDir}", n.NasShareAlt),
                Folder(@"\\nas\backup", n.NasBackups))));

        return Config(work, home, admin);
    }

    /// <summary>
    /// Long names in narrow fixed columns (150 and 160 DIPs): what an ellipsized name and path look like.
    /// </summary>
    public static AppConfig CreateNarrow(string language)
    {
        var n = SampleNames.For(language);
        var tab = Tab(n.LongTab, "📄",
            Column(150,
                Group(n.LongGroup, "📦", Blue, false,
                    Folder($@"D:\{n.ClientsDir}\Acme\{n.ContractDocs}", n.ContractDocs),
                    Folder(n.AcmePath),
                    File($@"D:\{n.ClientsDir}\Acme\{n.LongBrief}")),
                Group(n.Collapsed, Slate, true,
                    Folder($@"E:\{n.ArchiveDir}4"))),
            Column(160,
                Group(n.Tools, "🧰", Violet, false,
                    Folder($@"C:\{n.ToolsDir}\Scripts"),
                    File(@"C:\Windows\System32\drivers\etc\hosts"))));

        return Config(tab);
    }

    /// <summary>
    /// Twelve tabs over a two-column board: more tabs than one line of a narrow screen holds, so that the tab bar wraps.
    /// The active tab is on the second line.
    /// </summary>
    public static AppConfig CreateManyTabs(string language)
    {
        var n = SampleNames.For(language);

        // Some of them without an icon (by position).
        var icons = new Dictionary<int, string>
        {
            [0] = "💼", [1] = "🏠", [2] = "⚙️", [7] = "📷", [8] = "🎵", [9] = "💻", [11] = "📥",
        };

        var tabs = n.ManyTabs.Select((name, index) => Tab(name, icons.GetValueOrDefault(index),
            Column(Group(n.FolderGroup, Blue, false,
                Folder(@"D:\" + name, name),
                Special(SpecialFolderKey.Documents))),
            Column(Group(n.FileGroup, Amber, false,
                File(@"D:\" + name + @"\" + n.NotesFile))))).ToArray();

        var config = Config(tabs);
        config.Settings.ActiveTabId = tabs[8].Id;
        return config;
    }

    private static AppConfig Config(params Tab[] tabs)
    {
        var config = new AppConfig { Tabs = tabs.ToList() };
        config.Settings.FirstRunCompleted = true;
        config.Settings.ActiveTabId = tabs[0].Id;
        return config;
    }

    private static Tab Tab(string name, string? icon, params Column[] columns) => new() { Name = name, Icon = icon, Columns = columns.ToList() };

    private static Column Column(params Group[] groups) => new() { Groups = groups.ToList() };

    private static Column Column(int width, params Group[] groups) => new() { Width = width, Groups = groups.ToList() };

    private static Group Group(string name, string color, bool collapsed, params Item[] items) =>
        new() { Name = name, Color = color, Collapsed = collapsed, Items = items.ToList() };

    private static Group Group(string name, string icon, string color, bool collapsed, params Item[] items) =>
        new() { Name = name, Icon = icon, Color = color, Collapsed = collapsed, Items = items.ToList() };

    private static Item Folder(string path, string? name = null) => new() { Kind = ItemKind.Folder, Path = path, Name = name };

    private static Item File(string path) => new() { Kind = ItemKind.File, Path = path };

    private static Item Special(SpecialFolderKey key) => new() { Kind = ItemKind.Folder, Special = key };

    private static Item Site(string url, string? name = null) => new() { Kind = ItemKind.Url, Url = url, Name = name };

    private static Item Separator() => new() { Kind = ItemKind.Separator };
}
