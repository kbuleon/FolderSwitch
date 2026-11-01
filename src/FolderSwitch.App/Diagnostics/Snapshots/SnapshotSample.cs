using FolderSwitch.Core.Model;

namespace FolderSwitch.App.Diagnostics.Snapshots;

/// <summary>
/// The configuration the snapshots are drawn with: the board of the mockups (tab « Travail », three columns: Projets,
/// Clients / Personnel, Archives collapsed, Finance / Outils, Réseau collapsed; then « Maison » and « Admin »). Finance
/// holds one item of each kind: a folder, two files of types Windows has icons for, a site whose favicon is in the
/// snapshot's cache (<see cref="FaviconHost"/>) and one without. Names, paths and addresses are examples, in French like
/// the mockups: they are the user's content, never translated.
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

    /// <summary>The mockups' board, on « Travail », every column automatic.</summary>
    public static AppConfig Create()
    {
        var work = Tab("Travail",
            Column(
                Group("Projets", Blue, false,
                    Folder(@"C:\Claude Projects\FolderSwitch"),
                    Folder(@"D:\Clients\Acme\site-web", "Site Acme"),
                    Folder(@"D:\Design\Maquettes"),
                    File(@"D:\Clients\Acme\Cahier des charges.docx")),
                Group("Clients", Cyan, false,
                    Folder(@"D:\Clients\Acme"),
                    Folder(@"D:\Clients\Durand", "Durand & Fils"),
                    Folder(@"D:\Compta\Factures\2026", "Factures 2026"))),
            Column(
                Group("Personnel", Amber, false,
                    Special(SpecialFolderKey.Downloads),
                    Special(SpecialFolderKey.Documents),
                    Special(SpecialFolderKey.Desktop),
                    File(@"%USERPROFILE%\Documents\Notes de réunion.md")),
                Group("Archives", Slate, true,
                    Folder(@"E:\Archives\2024"),
                    Folder(@"E:\Archives\2025")),
                Group("Finance", Red, false,
                    Folder(@"D:\Finance\Relevés"),
                    File(@"D:\Finance\Budget 2026.xlsx"),
                    File(@"D:\Finance\Avis d'imposition 2025.pdf"),
                    Site("https://" + FaviconHost + "/fr/", "Morningstar"),
                    Site("https://www.boursorama.com/bourse/"))),
            Column(
                Group("Outils", Violet, false,
                    Folder(@"C:\Outils\Scripts"),
                    Folder(@"C:\ProgramData\MonApp\logs", "Logs"),
                    File(@"C:\Windows\System32\drivers\etc\hosts")),
                Group("Réseau", Green, true,
                    Folder(@"\\nas\partage", "Partage NAS"),
                    Folder(@"\\nas\photos", "Photos"))));

        var home = Tab("Maison",
            Column(Group("Papiers", Orange, false,
                Folder(@"D:\Perso\Maison\Factures", "Factures maison"),
                Folder(@"D:\Perso\Assurances"),
                File(@"D:\Perso\Maison\Plan travaux.pdf"))),
            Column(Group("Photos", Pink, false,
                Folder(@"D:\Photos\2025"),
                Folder(@"D:\Photos\2026"),
                Folder(@"D:\Photos\Albums"))),
            Column(Group("Loisirs", Green, true,
                Special(SpecialFolderKey.Music),
                Folder(@"%USERPROFILE%\Saved Games", "Sauvegardes jeux"))));

        var admin = Tab("Admin",
            Column(Group("Système", Slate, false,
                Special(SpecialFolderKey.ProgramFiles),
                Special(SpecialFolderKey.RoamingAppData),
                File(@"C:\Windows\System32\drivers\etc\hosts"))),
            Column(Group("Partages", Cyan, false,
                Folder(@"\\nas\partage", "NAS partage"),
                Folder(@"\\nas\backup", "NAS sauvegardes"))));

        return Config(work, home, admin);
    }

    /// <summary>
    /// Long names in narrow fixed columns (150 and 160 DIPs): what an ellipsized name and path look like.
    /// </summary>
    public static AppConfig CreateNarrow()
    {
        var tab = Tab("Un onglet au nom vraiment très long pour la barre",
            Column(150,
                Group("Un groupe au nom bien trop long pour sa colonne", Blue, false,
                    Folder(@"D:\Clients\Acme\Documents contractuels et annexes signées", "Documents contractuels et annexes signées"),
                    Folder(@"D:\Clients\Acme"),
                    File(@"D:\Clients\Acme\Cahier des charges version définitive.docx")),
                Group("Replié", Slate, true,
                    Folder(@"E:\Archives\2024"))),
            Column(160,
                Group("Outils", Violet, false,
                    Folder(@"C:\Outils\Scripts"),
                    File(@"C:\Windows\System32\drivers\etc\hosts"))));

        return Config(tab);
    }

    /// <summary>
    /// Twelve tabs over a two-column board: more tabs than one line of a narrow screen holds, so that the tab bar wraps.
    /// The active tab is on the second line.
    /// </summary>
    public static AppConfig CreateManyTabs()
    {
        string[] names =
        [
            "Travail", "Maison", "Admin", "Clients 2026", "Comptabilité", "Projets en cours",
            "Archives", "Photos", "Musique", "Développement", "Réseau", "Téléchargements",
        ];

        var tabs = names.Select(name => Tab(name,
            Column(Group("Dossiers", Blue, false,
                Folder(@"D:\" + name, name),
                Special(SpecialFolderKey.Documents))),
            Column(Group("Fichiers", Amber, false,
                File(@"D:\" + name + @"\Notes.md"))))).ToArray();

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

    private static Tab Tab(string name, params Column[] columns) => new() { Name = name, Columns = columns.ToList() };

    private static Column Column(params Group[] groups) => new() { Groups = groups.ToList() };

    private static Column Column(int width, params Group[] groups) => new() { Width = width, Groups = groups.ToList() };

    private static Group Group(string name, string color, bool collapsed, params Item[] items) =>
        new() { Name = name, Color = color, Collapsed = collapsed, Items = items.ToList() };

    private static Item Folder(string path, string? name = null) => new() { Kind = ItemKind.Folder, Path = path, Name = name };

    private static Item File(string path) => new() { Kind = ItemKind.File, Path = path };

    private static Item Special(SpecialFolderKey key) => new() { Kind = ItemKind.Folder, Special = key };

    private static Item Site(string url, string? name = null) => new() { Kind = ItemKind.Url, Url = url, Name = name };
}
