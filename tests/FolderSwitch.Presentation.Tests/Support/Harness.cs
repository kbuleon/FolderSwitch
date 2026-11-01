using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>
/// A <see cref="MainViewModel"/> over a known board, with fakes for every service.
/// <list type="bullet">
/// <item>Work: left column (Projects: Acme folder + Desktop; Inbox: empty), right column (empty).</item>
/// <item>Home (active): one column (Media: Music folder + Notes.txt file).</item>
/// <item>Misc: one empty column.</item>
/// </list>
/// </summary>
internal sealed class Harness
{
    /// <param name="arrange">Runs on the configuration before the view model is built.</param>
    public Harness(Action<Harness>? arrange = null)
    {
        Acme = new Item { Kind = ItemKind.Folder, Path = @"C:\Projects\Acme" };
        Desktop = new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.Desktop };
        Music = new Item { Kind = ItemKind.Folder, Path = @"D:\Music" };
        Notes = new Item { Kind = ItemKind.File, Path = @"D:\Notes.txt" };

        Projects = new Group { Name = "Projects", Color = "#3b82f6", Items = { Acme, Desktop } };
        Inbox = new Group { Name = "Inbox", Color = "#22c55e" };
        Media = new Group { Name = "Media", Color = "#ef4444", Items = { Music, Notes } };

        WorkLeft = new Column { Groups = { Projects, Inbox } };
        WorkRight = new Column();
        HomeMain = new Column { Groups = { Media } };
        MiscOnly = new Column();

        Work = new Tab { Name = "Work", Columns = { WorkLeft, WorkRight } };
        Home = new Tab { Name = "Home", Columns = { HomeMain } };
        Misc = new Tab { Name = "Misc", Columns = { MiscOnly } };

        Config = new AppConfig { Tabs = { Work, Home, Misc } };
        Config.Settings.ActiveTabId = Home.Id;
        arrange?.Invoke(this);

        Localizer = TestLocalizer.Create();
        Vm = new MainViewModel(Config, Activator, Persistence, Prompts, Host, Browser, Favicons, Localizer, Time);
    }

    public AppConfig Config { get; }

    public Tab Work { get; }

    public Tab Home { get; }

    public Tab Misc { get; }

    public Column WorkLeft { get; }

    public Column WorkRight { get; }

    public Column HomeMain { get; }

    public Column MiscOnly { get; }

    public Group Projects { get; }

    public Group Inbox { get; }

    public Group Media { get; }

    public Item Acme { get; }

    public Item Desktop { get; }

    public Item Music { get; }

    public Item Notes { get; }

    public FakeItemActivator Activator { get; } = new();

    public FakeConfigPersistence Persistence { get; } = new();

    public FakeUserPrompts Prompts { get; } = new();

    public FakeWindowHost Host { get; } = new();

    public FakeBrowserAddressReader Browser { get; } = new();

    public FakeFavicons Favicons { get; } = new();

    public ManualTimeProvider Time { get; } = new();

    public Localizer Localizer { get; }

    public MainViewModel Vm { get; }

    public TabViewModel TabVm(string name) => Vm.Tabs.Single(t => t.Name == name);

    public ColumnViewModel ColumnVm(string tabName, int index) => TabVm(tabName).Columns[index];

    public GroupViewModel GroupVm(string name) => AllGroups().Single(g => g.Name == name);

    public ItemViewModel ItemVm(string displayName) =>
        AllGroups().SelectMany(g => g.Items).Single(i => i.DisplayName == displayName);

    public MainViewModel Customizing()
    {
        Vm.IsCustomizing = true;
        return Vm;
    }

    private IEnumerable<GroupViewModel> AllGroups() =>
        Vm.Tabs.SelectMany(t => t.Columns).SelectMany(c => c.Groups);
}
