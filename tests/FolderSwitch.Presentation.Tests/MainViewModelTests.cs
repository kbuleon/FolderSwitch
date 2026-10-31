using System.ComponentModel;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

public class MainViewModelTests
{
    private const string ThisPcClsid = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string RecycleBinClsid = "::{645FF040-5081-101B-9F08-00AA002F954E}";

    private static readonly CallContext ExplorerContext = new(CallContextKind.Explorer, 42, @"C:\Users\Me\Work");
    private static readonly CallContext DialogContext = new(CallContextKind.FileDialog, 43, @"D:\Out");

    private static List<string?> RecordChanges(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    // ----- Showing, hiding, mode -----

    [Fact]
    public void OnShown_resets_to_normal_mode_and_last_active_tab()
    {
        var h = new Harness();
        var vm = h.Customizing();
        vm.ActiveTab = h.TabVm("Misc");
        var projects = h.GroupVm("Projects");
        vm.BeginRename(projects);
        vm.Selected = projects;

        vm.OnShown(ExplorerContext);

        Assert.False(vm.IsCustomizing);
        Assert.False(projects.IsRenaming);
        Assert.Null(vm.Selected);
        Assert.Same(h.TabVm("Misc"), vm.ActiveTab);
        Assert.Equal(h.Misc.Id, h.Config.Settings.ActiveTabId);
        Assert.Same(ExplorerContext, vm.Context);
    }

    [Fact]
    public void ContextLine_follows_context()
    {
        var h = new Harness();
        var vm = h.Vm;
        var changes = RecordChanges(vm);

        vm.OnShown(ExplorerContext);
        Assert.Equal("Called from File Explorer", vm.ContextLine);

        vm.OnShown(DialogContext);
        Assert.Equal("Called from a file dialog", vm.ContextLine);

        vm.OnShown(CallContext.None);
        Assert.Null(vm.ContextLine);

        Assert.Equal(3, changes.Count(name => name == nameof(MainViewModel.ContextLine)));
    }

    [Fact]
    public void Context_starts_as_none()
    {
        var h = new Harness();

        Assert.Same(CallContext.None, h.Vm.Context);
        Assert.Null(h.Vm.ContextLine);
        Assert.False(h.Vm.IsCustomizing);
    }

    [Fact]
    public void CustomizeLabel_follows_mode()
    {
        var h = new Harness();
        var vm = h.Vm;
        var changes = RecordChanges(vm);

        Assert.Equal("Customize", vm.CustomizeLabel);

        vm.ToggleCustomizeCommand.Execute(null);
        Assert.True(vm.IsCustomizing);
        Assert.Equal("Done", vm.CustomizeLabel);

        vm.ToggleCustomize();
        Assert.False(vm.IsCustomizing);
        Assert.Equal("Customize", vm.CustomizeLabel);
        Assert.Equal(2, changes.Count(name => name == nameof(MainViewModel.CustomizeLabel)));
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void Deactivation_hides_only_in_normal_mode()
    {
        var h = new Harness();
        var vm = h.Vm;

        vm.OnDeactivated();
        Assert.Equal(1, h.Host.HideCount);

        vm.IsCustomizing = true;
        vm.OnDeactivated();
        Assert.Equal(1, h.Host.HideCount);
    }

    [Fact]
    public async Task Deactivation_while_a_prompt_is_open_does_not_hide()
    {
        // A message box takes the focus from the main window: that must not count as "clicked elsewhere".
        var h = new Harness();
        var vm = h.Vm;
        h.Prompts.DuringPrompt = vm.OnDeactivated;
        h.Activator.Outcome = ActivationOutcome.TargetUnavailable;

        await vm.ActivateItemAsync(h.ItemVm("Music"), ctrl: false);

        Assert.Single(h.Prompts.Errors);
        Assert.Equal(0, h.Host.HideCount);

        vm.OnDeactivated();
        Assert.Equal(1, h.Host.HideCount);
    }

    [Fact]
    public void Close_hides_the_window_in_both_modes()
    {
        var h = new Harness();
        var vm = h.Vm;

        vm.CloseCommand.Execute(null);
        vm.IsCustomizing = true;
        vm.Close();

        Assert.Equal(2, h.Host.HideCount);
    }

    [Fact]
    public void Leaving_customize_mode_cancels_the_rename_and_the_selection()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var inbox = h.GroupVm("Inbox");
        vm.BeginRename(inbox);

        vm.ToggleCustomize();

        Assert.False(inbox.IsRenaming);
        Assert.Null(vm.Selected);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    // ----- Active tab: recorded, followed, and replaced when its tab goes -----

    [Fact]
    public void ActiveTab_follows_settings_active_tab_id()
    {
        var h = new Harness();

        Assert.Same(h.TabVm("Home"), h.Vm.ActiveTab);
        Assert.Equal(["Work", "Home", "Misc"], h.Vm.Tabs.Select(t => t.Name));
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveTab_falls_back_to_first_tab_when_id_is_unknown_or_missing(bool missing)
    {
        var h = new Harness(board => board.Config.Settings.ActiveTabId = missing ? null : Guid.NewGuid());

        Assert.Same(h.TabVm("Work"), h.Vm.ActiveTab);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void Changing_the_active_tab_updates_settings_and_saves()
    {
        var h = new Harness();
        var vm = h.Vm;
        var changes = RecordChanges(vm);

        vm.ActiveTab = h.TabVm("Work");

        Assert.Equal(h.Work.Id, h.Config.Settings.ActiveTabId);
        Assert.Equal(1, h.Persistence.SaveCount);
        Assert.Contains(nameof(MainViewModel.ActiveTab), changes);

        vm.ActiveTab = h.TabVm("Work");
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void ActiveTab_ignores_a_tab_that_is_not_on_the_board()
    {
        var h = new Harness();
        var vm = h.Vm;
        var home = vm.ActiveTab;

        vm.ActiveTab = null!;

        Assert.Same(home, vm.ActiveTab);
        Assert.Equal(h.Home.Id, h.Config.Settings.ActiveTabId);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void NextTab_wraps_around()
    {
        var h = new Harness();
        var vm = h.Vm;

        vm.NextTab();
        Assert.Same(h.TabVm("Misc"), vm.ActiveTab);

        vm.NextTabCommand.Execute(null);
        Assert.Same(h.TabVm("Work"), vm.ActiveTab);

        vm.PreviousTabCommand.Execute(null);
        Assert.Same(h.TabVm("Misc"), vm.ActiveTab);

        vm.PreviousTab();
        Assert.Same(h.TabVm("Home"), vm.ActiveTab);

        Assert.Equal(h.Home.Id, h.Config.Settings.ActiveTabId);
        Assert.Equal(4, h.Persistence.SaveCount);
    }

    [Fact]
    public void Removing_the_active_tab_activates_the_next_one()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.RemoveTab(h.TabVm("Home"));

        Assert.Same(h.TabVm("Misc"), vm.ActiveTab);
        Assert.Equal(h.Misc.Id, h.Config.Settings.ActiveTabId);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void Removing_the_active_last_tab_activates_the_previous_one()
    {
        var h = new Harness();
        var vm = h.Customizing();
        vm.ActiveTab = h.TabVm("Misc");

        vm.RemoveTab(h.TabVm("Misc"));

        Assert.Same(h.TabVm("Home"), vm.ActiveTab);
        Assert.Equal(h.Home.Id, h.Config.Settings.ActiveTabId);
        Assert.Equal(["Work", "Home"], vm.Tabs.Select(t => t.Name));
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void Removing_another_tab_keeps_the_active_one()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.RemoveTab(h.TabVm("Misc"));

        Assert.Same(h.TabVm("Home"), vm.ActiveTab);
        Assert.Equal(h.Home.Id, h.Config.Settings.ActiveTabId);
    }

    // ----- Reload: an imported configuration replaces the board -----

    [Fact]
    public void Reload_rebuilds_from_the_new_config_and_keeps_the_mode()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var tabs = vm.Tabs;
        vm.BeginRename(h.GroupVm("Projects"));
        var first = new Tab { Name = "Imported A", Columns = { new Column() } };
        var second = new Tab
        {
            Name = "Imported B",
            Columns = { new Column { Groups = { new Group { Name = "Tools", Color = "#64748b" } } } },
        };
        var imported = new AppConfig { Tabs = { first, second } };
        imported.Settings.ActiveTabId = second.Id;

        vm.Reload(imported);

        Assert.Same(tabs, vm.Tabs);
        Assert.Equal(["Imported A", "Imported B"], vm.Tabs.Select(t => t.Name));
        Assert.Same(vm.Tabs[1], vm.ActiveTab);
        Assert.Equal("Tools", Assert.Single(Assert.Single(vm.ActiveTab.Columns).Groups).Name);
        Assert.True(vm.IsCustomizing);
        Assert.Null(vm.Selected);
        Assert.Equal(0, h.Persistence.SaveCount);

        vm.AddColumn();

        Assert.Equal(2, second.Columns.Count);
        Assert.Single(h.Home.Columns);
        Assert.Same(imported, Assert.Single(h.Persistence.Saved));
    }

    [Fact]
    public void Reload_with_an_unknown_active_tab_starts_on_the_first_tab()
    {
        var h = new Harness();
        var imported = new AppConfig { Tabs = { new Tab { Name = "Only" } } };

        h.Vm.Reload(imported);

        Assert.Equal("Only", h.Vm.ActiveTab.Name);
    }

    [Fact]
    public void A_config_without_tabs_is_refused()
    {
        var h = new Harness();

        Assert.Throws<ArgumentException>(() => h.Vm.Reload(new AppConfig()));
    }

    // ----- Normal use -----

    [Fact]
    public void ToggleGroup_saves_immediately_in_normal_mode()
    {
        var h = new Harness();
        var vm = h.Vm;
        var media = h.GroupVm("Media");
        var changes = RecordChanges(media);

        vm.ToggleGroup(media);

        Assert.True(media.IsCollapsed);
        Assert.True(h.Media.Collapsed);
        Assert.Same(h.Config, Assert.Single(h.Persistence.Saved));
        Assert.Contains(nameof(GroupViewModel.IsCollapsed), changes);

        vm.ToggleGroupCommand.Execute(media);

        Assert.False(media.IsCollapsed);
        Assert.False(h.Media.Collapsed);
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void ToggleGroup_also_saves_in_customize_mode()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.ToggleGroup(h.GroupVm("Inbox"));

        Assert.True(h.Inbox.Collapsed);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public async Task ActivateItem_hides_on_success()
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(ExplorerContext);

        await vm.ActivateItemAsync(h.ItemVm("Music"), ctrl: true);

        var call = Assert.Single(h.Activator.Calls);
        Assert.Same(h.Music, call.Item);
        Assert.Same(ExplorerContext, call.Context);
        Assert.True(call.Ctrl);
        Assert.Equal(1, h.Host.HideCount);
        Assert.Empty(h.Prompts.Errors);
    }

    // ----- The item's menu -----

    [Fact]
    public async Task OpenInExplorer_asks_for_a_new_window_and_hides()
    {
        var h = new Harness();
        h.Vm.OnShown(ExplorerContext);
        h.Activator.Outcome = ActivationOutcome.OpenedElsewhere;

        await h.Vm.OpenInExplorerAsync(h.ItemVm("Notes.txt"));

        Assert.Equal([h.Notes], h.Activator.ExplorerCalls);
        Assert.Empty(h.Activator.Calls);
        Assert.Equal(1, h.Host.HideCount);
    }

    [Fact]
    public async Task OpenInExplorer_keeps_window_and_shows_why_it_failed()
    {
        var h = new Harness();
        h.Activator.Outcome = ActivationOutcome.TargetUnavailable;

        await h.Vm.OpenInExplorerAsync(h.ItemVm("Music"));

        Assert.Equal(["Error.TargetUnavailable"], h.Prompts.Errors);
        Assert.Equal(0, h.Host.HideCount);
    }

    [Fact]
    public async Task OpenInExplorer_does_nothing_in_Customize_mode()
    {
        var h = new Harness();
        h.Vm.ToggleCustomize();

        await h.Vm.OpenInExplorerAsync(h.ItemVm("Music"));

        Assert.Empty(h.Activator.ExplorerCalls);
    }

    [Fact]
    public void A_new_tab_is_offered_when_Ctrl_click_opens_one_and_not_in_Customize_mode()
    {
        var h = new Harness();
        var music = h.ItemVm("Music");

        Assert.False(h.Vm.CanOpenInNewTab(music));

        h.Activator.NewTabPossible = true;
        Assert.True(h.Vm.CanOpenInNewTab(music));

        h.Vm.ToggleCustomize();
        Assert.False(h.Vm.CanOpenInNewTab(music));
    }

    [Fact]
    public async Task ActivateItem_keeps_window_and_shows_error_when_unavailable()
    {
        var h = new Harness();
        h.Activator.Outcome = ActivationOutcome.TargetUnavailable;

        await h.Vm.ActivateItemAsync(h.ItemVm("Notes.txt"), ctrl: false);

        Assert.Equal(["Error.TargetUnavailable"], h.Prompts.Errors);
        Assert.Equal(0, h.Host.HideCount);
    }

    [Fact]
    public async Task ActivateItem_keeps_window_and_shows_error_when_the_action_fails()
    {
        var h = new Harness();
        h.Activator.Outcome = ActivationOutcome.Failed;

        await h.Vm.ActivateItemAsync(h.ItemVm("Music"), ctrl: false);

        Assert.Equal(["Error.ActionFailed"], h.Prompts.Errors);
        Assert.Equal(0, h.Host.HideCount);
    }

    [Fact]
    public async Task ActivateItem_hides_when_the_item_opened_elsewhere()
    {
        var h = new Harness();
        h.Activator.Outcome = ActivationOutcome.OpenedElsewhere;

        await h.Vm.ActivateItemAsync(h.ItemVm("Notes.txt"), ctrl: false);

        Assert.Equal(1, h.Host.HideCount);
        Assert.Empty(h.Prompts.Errors);
    }

    [Fact]
    public async Task ActivateItem_adds_nothing_to_the_shell_s_own_error_message()
    {
        var h = new Harness();
        h.Activator.Outcome = ActivationOutcome.ReportedByShell;

        await h.Vm.ActivateItemAsync(h.ItemVm("Notes.txt"), ctrl: false);

        Assert.Empty(h.Prompts.Errors);
        Assert.Equal(0, h.Host.HideCount);
    }

    [Fact]
    public async Task ActivateItem_is_ignored_in_customize_mode()
    {
        var h = new Harness();
        var vm = h.Customizing();

        await vm.ActivateItemAsync(h.ItemVm("Music"), ctrl: false);

        Assert.Empty(h.Activator.Calls);
        Assert.Equal(0, h.Host.HideCount);
    }

    [Fact]
    public async Task ActivateItem_ignores_clicks_while_one_is_pending()
    {
        var h = new Harness();
        var vm = h.Vm;
        var music = h.ItemVm("Music");
        var pending = new TaskCompletionSource<ActivationOutcome>();
        h.Activator.Pending = pending.Task;

        var first = vm.ActivateItemAsync(music, ctrl: false);
        await vm.ActivateItemAsync(music, ctrl: false);

        Assert.Single(h.Activator.Calls);

        pending.SetResult(ActivationOutcome.Done);
        await first;
        Assert.Equal(1, h.Host.HideCount);

        h.Activator.Pending = null;
        await vm.ActivateItemAsync(music, ctrl: false);
        Assert.Equal(2, h.Activator.Calls.Count);
    }

    [Theory]
    [InlineData(ActivationOutcome.Done)]
    [InlineData(ActivationOutcome.OpenedElsewhere)]
    [InlineData(ActivationOutcome.TargetUnavailable)]
    [InlineData(ActivationOutcome.Failed)]
    public async Task ActivateItem_outcome_is_dropped_when_the_window_was_shown_again(ActivationOutcome outcome)
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(ExplorerContext);
        var pending = new TaskCompletionSource<ActivationOutcome>();
        h.Activator.Pending = pending.Task;

        var click = vm.ActivateItemAsync(h.ItemVm("Music"), ctrl: false);
        vm.OnShown(DialogContext);
        pending.SetResult(outcome);
        await click;

        Assert.Equal(0, h.Host.HideCount);
        Assert.Empty(h.Prompts.Errors);
    }

    [Fact]
    public async Task ActivateItem_ignores_an_item_that_is_no_longer_on_the_board()
    {
        var h = new Harness();
        var vm = h.Vm;
        var stale = h.ItemVm("Music");
        vm.Reload(new AppConfig { Tabs = { new Tab { Name = "Only", Columns = { new Column() } } } });

        await vm.ActivateItemAsync(stale, ctrl: false);

        Assert.Empty(h.Activator.Calls);
        Assert.Equal(0, h.Host.HideCount);
    }

    // ----- Customize mode: guards -----

    [Fact]
    public void Customize_commands_do_nothing_in_normal_mode()
    {
        var h = new Harness();
        var vm = h.Vm;
        var raised = 0;
        vm.AddTabCommand.CanExecuteChanged += (_, _) => raised++;

        Assert.False(vm.AddTabCommand.CanExecute(null));
        Assert.False(vm.AddGroupCommand.CanExecute(h.ColumnVm("Work", 1)));

        vm.AddTab();
        vm.AddColumn();
        vm.AddGroup(h.ColumnVm("Work", 1));
        vm.RemoveItem(h.ItemVm("Music"));
        vm.Selected = h.GroupVm("Media");
        vm.RenameSelected();

        Assert.Equal(3, vm.Tabs.Count);
        Assert.Single(h.Home.Columns);
        Assert.Empty(h.WorkRight.Groups);
        Assert.Equal(2, h.Media.Items.Count);
        Assert.False(h.GroupVm("Media").IsRenaming);
        Assert.Equal(0, h.Persistence.SaveCount);

        vm.IsCustomizing = true;

        Assert.True(vm.AddTabCommand.CanExecute(null));
        Assert.True(raised > 0);
    }

    [Fact]
    public void Commands_on_view_models_from_before_a_reload_do_nothing()
    {
        // A view model left over from before a Reload belongs to a configuration that is no longer edited.
        var h = new Harness();
        var vm = h.Customizing();
        var staleColumn = h.ColumnVm("Work", 1);
        var staleGroup = h.GroupVm("Inbox");
        vm.Reload(new AppConfig { Tabs = { new Tab { Name = "Only", Columns = { new Column() } } } });

        vm.AddGroup(staleColumn);
        vm.AddSpecialFolder(staleGroup, SpecialFolderKey.Documents);
        vm.SetColumnWidth(staleColumn, 300);

        Assert.Empty(h.WorkRight.Groups);
        Assert.Empty(h.Inbox.Items);
        Assert.Null(h.WorkRight.Width);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Theory]
    [InlineData("AddTab")]
    [InlineData("RemoveTab")]
    [InlineData("MoveTab")]
    [InlineData("AddColumn")]
    [InlineData("RemoveColumn")]
    [InlineData("SetColumnWidth")]
    [InlineData("AddGroup")]
    [InlineData("RemoveGroup")]
    [InlineData("MoveGroup")]
    [InlineData("SetGroupColor")]
    [InlineData("AddFolder")]
    [InlineData("AddSpecialFolder")]
    [InlineData("AddFile")]
    [InlineData("AddCurrentFolder")]
    [InlineData("RemoveItem")]
    [InlineData("MoveItem")]
    [InlineData("Rename")]
    public void Every_customize_command_saves_once(string command)
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(ExplorerContext);
        vm.IsCustomizing = true;
        h.Prompts.FolderToPick = @"E:\Picked";
        h.Prompts.FileToPick = @"E:\Picked\report.pdf";

        Action run = command switch
        {
            "AddTab" => () => vm.AddTabCommand.Execute(null),
            "RemoveTab" => () => vm.RemoveTabCommand.Execute(h.TabVm("Work")),
            "MoveTab" => () => vm.MoveTab(h.TabVm("Misc"), 0),
            "AddColumn" => () => vm.AddColumnCommand.Execute(null),
            "RemoveColumn" => () => vm.RemoveColumnCommand.Execute(h.ColumnVm("Work", 1)),
            "SetColumnWidth" => () => vm.SetColumnWidth(h.ColumnVm("Work", 0), 300),
            "AddGroup" => () => vm.AddGroupCommand.Execute(h.ColumnVm("Work", 1)),
            "RemoveGroup" => () => vm.RemoveGroupCommand.Execute(h.GroupVm("Projects")),
            "MoveGroup" => () => vm.MoveGroup(h.GroupVm("Projects"), h.ColumnVm("Work", 1), 0),
            "SetGroupColor" => () => vm.SetGroupColor(h.GroupVm("Inbox"), "#8b5cf6"),
            "AddFolder" => () => vm.AddFolderCommand.Execute(h.GroupVm("Inbox")),
            "AddSpecialFolder" => () => vm.AddSpecialFolder(h.GroupVm("Inbox"), SpecialFolderKey.Downloads),
            "AddFile" => () => vm.AddFileCommand.Execute(h.GroupVm("Inbox")),
            "AddCurrentFolder" => () => vm.AddCurrentFolderCommand.Execute(h.GroupVm("Inbox")),
            "RemoveItem" => () => vm.RemoveItemCommand.Execute(h.ItemVm("Acme")),
            "MoveItem" => () => vm.MoveItem(h.ItemVm("Acme"), h.GroupVm("Media"), 0),
            "Rename" => () =>
            {
                vm.BeginRenameCommand.Execute(h.TabVm("Work"));
                vm.CommitRename(h.TabVm("Work"), "Office");
            },
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        run();

        Assert.Same(h.Config, Assert.Single(h.Persistence.Saved));
    }

    // ----- Tabs -----

    [Fact]
    public void AddTab_enters_rename_and_becomes_active()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.AddTab();

        var added = vm.Tabs[^1];
        Assert.Equal("New tab", added.Name);
        Assert.True(added.IsRenaming);
        Assert.Same(added, vm.Selected);
        Assert.Same(added, vm.ActiveTab);
        Assert.Equal(2, added.Columns.Count);
        var model = h.Config.Tabs[^1];
        Assert.Equal("New tab", model.Name);
        Assert.Equal(model.Id, h.Config.Settings.ActiveTabId);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void RemoveTab_with_groups_asks_confirmation_and_respects_no()
    {
        var h = new Harness();
        var vm = h.Customizing();
        h.Prompts.ConfirmAnswer = false;

        vm.RemoveTab(h.TabVm("Work"));

        Assert.Equal(["Confirm.DeleteTab"], h.Prompts.Confirmations);
        Assert.Equal(3, vm.Tabs.Count);
        Assert.Contains(h.Work, h.Config.Tabs);
        Assert.Equal(0, h.Persistence.SaveCount);

        h.Prompts.ConfirmAnswer = true;
        vm.RemoveTab(h.TabVm("Work"));

        Assert.Equal(["Home", "Misc"], vm.Tabs.Select(t => t.Name));
        Assert.DoesNotContain(h.Work, h.Config.Tabs);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void RemoveTab_without_groups_does_not_ask()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.RemoveTab(h.TabVm("Misc"));

        Assert.Empty(h.Prompts.Confirmations);
        Assert.DoesNotContain(h.Misc, h.Config.Tabs);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void RemoveTab_of_the_last_tab_shows_error_without_asking()
    {
        var h = new Harness();
        var vm = h.Customizing();
        vm.RemoveTab(h.TabVm("Misc"));
        vm.RemoveTab(h.TabVm("Work"));
        h.Prompts.Confirmations.Clear();

        vm.RemoveTab(h.TabVm("Home"));

        Assert.Empty(h.Prompts.Confirmations);
        Assert.Equal(["Error.LastTab"], h.Prompts.Errors);
        Assert.Same(h.TabVm("Home"), Assert.Single(vm.Tabs));
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void MoveTab_reorders_view_models_like_the_model()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.MoveTab(h.TabVm("Misc"), 0);

        Assert.Equal(["Misc", "Work", "Home"], vm.Tabs.Select(t => t.Name));
        Assert.Equal(["Misc", "Work", "Home"], h.Config.Tabs.Select(t => t.Name));
        Assert.Same(h.TabVm("Home"), vm.ActiveTab);
    }

    // ----- Columns -----

    [Fact]
    public void AddColumn_adds_to_the_active_tab()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.AddColumn();

        Assert.Equal(2, h.Home.Columns.Count);
        Assert.Equal(2, h.TabVm("Home").Columns.Count);
        Assert.True(h.TabVm("Home").Columns[1].IsEmpty);
        Assert.Same(h.TabVm("Home"), h.TabVm("Home").Columns[1].Tab);
    }

    [Fact]
    public void RemoveColumn_non_empty_shows_error()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.RemoveColumn(h.ColumnVm("Work", 0));

        Assert.Equal(["Error.ColumnNotEmpty"], h.Prompts.Errors);
        Assert.Equal(2, h.TabVm("Work").Columns.Count);
        Assert.Equal(2, h.Work.Columns.Count);
        Assert.Equal(0, h.Persistence.SaveCount);

        vm.RemoveColumn(h.ColumnVm("Work", 1));

        Assert.Single(h.TabVm("Work").Columns);
        Assert.Equal([h.WorkLeft], h.Work.Columns);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void SetColumnWidth_clamps_and_can_return_to_automatic()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var left = h.ColumnVm("Work", 0);
        var changes = RecordChanges(left);

        vm.SetColumnWidth(left, 333);

        Assert.Equal(330, left.Width);
        Assert.Equal(330, h.WorkLeft.Width);
        Assert.Contains(nameof(ColumnViewModel.Width), changes);

        vm.SetColumnWidth(left, null);

        Assert.Null(left.Width);
        Assert.Null(h.WorkLeft.Width);
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    // ----- Groups -----

    [Fact]
    public void AddGroup_enters_rename()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var right = h.ColumnVm("Work", 1);
        var changes = RecordChanges(right);

        vm.AddGroup(right);

        var group = Assert.Single(right.Groups);
        Assert.Equal("New group", group.Name);
        Assert.True(group.IsRenaming);
        Assert.Same(group, vm.Selected);
        Assert.Same(right, group.Column);
        Assert.False(right.IsEmpty);
        Assert.Contains(nameof(ColumnViewModel.IsEmpty), changes);
        Assert.Equal("New group", Assert.Single(h.WorkRight.Groups).Name);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddGroup_color_is_the_next_palette_color_for_the_tab()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var right = h.ColumnVm("Work", 1);

        for (var i = 0; i < 10; i++)
        {
            vm.AddGroup(right);
        }

        // Work already holds two groups: the new ones take palette colors 2, 3, ... and wrap after the tenth.
        var expected = Enumerable.Range(2, 10).Select(i => ThemeCatalog.GroupPalette[i % 10].Hex);
        Assert.Equal(expected, right.Groups.Select(g => g.Color));
        Assert.Equal(expected, h.WorkRight.Groups.Select(g => g.Color));

        vm.AddGroup(h.ColumnVm("Misc", 0));
        Assert.Equal(ThemeCatalog.GroupPalette[0].Hex, h.MiscOnly.Groups[0].Color);
    }

    [Fact]
    public void RemoveGroup_with_items_asks_confirmation()
    {
        var h = new Harness();
        var vm = h.Customizing();
        h.Prompts.ConfirmAnswer = false;

        vm.RemoveGroup(h.GroupVm("Projects"));

        Assert.Equal(["Confirm.DeleteGroup"], h.Prompts.Confirmations);
        Assert.Contains(h.Projects, h.WorkLeft.Groups);
        Assert.Equal(0, h.Persistence.SaveCount);

        h.Prompts.ConfirmAnswer = true;
        vm.RemoveGroup(h.GroupVm("Projects"));

        Assert.Equal(["Inbox"], h.ColumnVm("Work", 0).Groups.Select(g => g.Name));
        Assert.Equal([h.Inbox], h.WorkLeft.Groups);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void RemoveGroup_without_items_does_not_ask()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.RemoveGroup(h.GroupVm("Inbox"));

        Assert.Empty(h.Prompts.Confirmations);
        Assert.Equal([h.Projects], h.WorkLeft.Groups);
    }

    [Fact]
    public void MoveGroup_moves_the_view_model_like_the_model()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var projects = h.GroupVm("Projects");
        var left = h.ColumnVm("Work", 0);
        var right = h.ColumnVm("Work", 1);

        vm.MoveGroup(projects, right, 0);

        Assert.Equal(["Inbox"], left.Groups.Select(g => g.Name));
        Assert.Same(projects, Assert.Single(right.Groups));
        Assert.Same(right, projects.Column);
        Assert.Equal([h.Projects], h.WorkRight.Groups);
        Assert.False(right.IsEmpty);

        vm.MoveGroup(h.GroupVm("Inbox"), right, 5);

        Assert.Equal(["Projects", "Inbox"], right.Groups.Select(g => g.Name));
        Assert.True(left.IsEmpty);
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void MoveGroup_to_another_tab_is_refused()
    {
        var h = new Harness();
        var vm = h.Customizing();

        vm.MoveGroup(h.GroupVm("Projects"), h.ColumnVm("Home", 0), 0);

        Assert.Contains(h.Projects, h.WorkLeft.Groups);
        Assert.Equal(["Media"], h.ColumnVm("Home", 0).Groups.Select(g => g.Name));
        Assert.Empty(h.Prompts.Errors);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void SetGroupColor_updates_the_color_and_ignores_a_malformed_one()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var inbox = h.GroupVm("Inbox");
        var changes = RecordChanges(inbox);

        vm.SetGroupColor(inbox, "#8b5cf6");

        Assert.Equal("#8b5cf6", inbox.Color);
        Assert.Equal("#8b5cf6", h.Inbox.Color);
        Assert.Contains(nameof(GroupViewModel.Color), changes);

        vm.SetGroupColor(inbox, "violet");
        vm.SetGroupColor(inbox, "#ff8b5cf6");

        Assert.Equal("#8b5cf6", h.Inbox.Color);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    // ----- Items -----

    [Fact]
    public void Item_texts_come_from_the_model()
    {
        var h = new Harness();

        var acme = h.ItemVm("Acme");
        Assert.Equal(@"C:\Projects\Acme", acme.SecondaryText);
        Assert.True(acme.IsFolder);

        var desktop = h.ItemVm("Desktop");
        Assert.Equal(@"%USERPROFILE%\Desktop", desktop.SecondaryText);
        Assert.True(desktop.IsFolder);

        var notes = h.ItemVm("Notes.txt");
        Assert.Equal(@"D:\Notes.txt", notes.SecondaryText);
        Assert.False(notes.IsFolder);

        Assert.Same(h.GroupVm("Media"), notes.Group);
        Assert.Equal(2, h.GroupVm("Media").Count);
    }

    [Fact]
    public void AddFolder_uses_the_folder_picker_and_a_cancel_adds_nothing()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var inbox = h.GroupVm("Inbox");

        vm.AddFolder(inbox);

        Assert.Empty(h.Inbox.Items);
        Assert.Equal(0, h.Persistence.SaveCount);

        h.Prompts.FolderToPick = @"E:\Archive";
        vm.AddFolder(inbox);

        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal(ItemKind.Folder, item.Kind);
        Assert.Equal(@"E:\Archive", item.Path);
        Assert.Null(item.Special);
        Assert.Null(item.Name);
        var added = Assert.Single(inbox.Items);
        Assert.Equal("Archive", added.DisplayName);
        Assert.Equal(1, inbox.Count);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddFile_uses_the_file_picker()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var inbox = h.GroupVm("Inbox");

        vm.AddFile(inbox);
        Assert.Empty(h.Inbox.Items);

        h.Prompts.FileToPick = @"E:\Plans\budget.xlsx";
        vm.AddFile(inbox);

        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal(ItemKind.File, item.Kind);
        Assert.Equal(@"E:\Plans\budget.xlsx", item.Path);
        Assert.False(Assert.Single(inbox.Items).IsFolder);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddSpecialFolder_adds_the_reference_and_skips_one_already_present()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var projects = h.GroupVm("Projects");

        vm.AddSpecialFolder(projects, SpecialFolderKey.Desktop);

        Assert.Equal(2, h.Projects.Items.Count);
        Assert.Equal(0, h.Persistence.SaveCount);

        vm.AddSpecialFolder(projects, SpecialFolderKey.ThisPC);

        var item = h.Projects.Items[^1];
        Assert.Equal(ItemKind.Folder, item.Kind);
        Assert.Equal(SpecialFolderKey.ThisPC, item.Special);
        Assert.Null(item.Path);
        Assert.Null(item.Name);
        Assert.Equal("This PC", projects.Items[^1].DisplayName);
        Assert.Equal("shell:MyComputerFolder", projects.Items[^1].SecondaryText);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddCurrentFolder_disabled_without_current_folder()
    {
        var h = new Harness();
        var vm = h.Vm;
        var inbox = h.GroupVm("Inbox");
        vm.OnShown(CallContext.None);
        vm.IsCustomizing = true;

        Assert.False(vm.AddCurrentFolderCommand.CanExecute(inbox));
        vm.AddCurrentFolder(inbox);
        Assert.Empty(h.Inbox.Items);
        Assert.Equal(0, h.Persistence.SaveCount);

        var raised = 0;
        vm.AddCurrentFolderCommand.CanExecuteChanged += (_, _) => raised++;
        vm.OnShown(ExplorerContext);
        vm.IsCustomizing = true;

        Assert.True(raised > 0);
        Assert.True(vm.AddCurrentFolderCommand.CanExecute(inbox));
        vm.AddCurrentFolderCommand.Execute(inbox);

        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal(ItemKind.Folder, item.Kind);
        Assert.Equal(@"C:\Users\Me\Work", item.Path);
        Assert.Null(item.Special);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Theory]
    [InlineData(ThisPcClsid, SpecialFolderKey.ThisPC)]
    [InlineData("::{20d04fe0-3aea-1069-a2d8-08002b30309d}", SpecialFolderKey.ThisPC)]
    [InlineData(RecycleBinClsid, SpecialFolderKey.RecycleBin)]
    public void AddCurrentFolder_maps_shell_locations_to_special_folders(string currentFolder, SpecialFolderKey expected)
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(new CallContext(CallContextKind.Explorer, 7, currentFolder));
        vm.IsCustomizing = true;

        vm.AddCurrentFolder(h.GroupVm("Inbox"));

        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal(expected, item.Special);
        Assert.Null(item.Path);
        Assert.Equal(ItemKind.Folder, item.Kind);
    }

    [Fact]
    public void AddCurrentFolder_keeps_a_unc_path_as_is()
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(new CallContext(CallContextKind.FileDialog, 7, @"\\nas\share\Team"));
        vm.IsCustomizing = true;

        vm.AddCurrentFolder(h.GroupVm("Inbox"));

        Assert.Equal(@"\\nas\share\Team", Assert.Single(h.Inbox.Items).Path);
    }

    [Theory]
    [InlineData("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}", "Home")]
    [InlineData(@"::{031E4825-7B94-4DC3-B131-E946B44C8DD5}\Documents.library-ms", "Documents")]
    [InlineData("shell:Downloads", "Downloads")]
    public void AddCurrentFolder_names_a_virtual_location_as_the_shell_shows_it(string currentFolder, string shellName)
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(new CallContext(CallContextKind.Explorer, 7, currentFolder, shellName));
        vm.IsCustomizing = true;

        vm.AddCurrentFolder(h.GroupVm("Inbox"));

        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal(currentFolder, item.Path);
        Assert.Equal(shellName, item.Name);
        Assert.Equal(shellName, h.ItemVm(shellName).DisplayName);
    }

    [Fact]
    public void AddCurrentFolder_keeps_the_default_name_of_a_path()
    {
        // A path names itself (its last segment); the shell's name, « Work » here, is not stored.
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(new CallContext(CallContextKind.Explorer, 7, @"C:\Users\Me\Work", "Work"));
        vm.IsCustomizing = true;

        vm.AddCurrentFolder(h.GroupVm("Inbox"));

        var item = Assert.Single(h.Inbox.Items);
        Assert.Null(item.Name);
    }

    [Fact]
    public void AddCurrentFolder_without_a_shell_name_keeps_the_virtual_location_unnamed()
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(new CallContext(CallContextKind.FileDialog, 7, "::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}"));
        vm.IsCustomizing = true;

        vm.AddCurrentFolder(h.GroupVm("Inbox"));

        Assert.Null(Assert.Single(h.Inbox.Items).Name);
    }

    [Theory]
    [InlineData("search-ms:displayname=Résultats&crumb=location:C%3A%5CUsers")]
    [InlineData("SEARCH-MS:query=rapport")]
    public void AddCurrentFolder_is_not_offered_for_search_results(string currentFolder)
    {
        var h = new Harness();
        var vm = h.Vm;
        var inbox = h.GroupVm("Inbox");
        vm.OnShown(new CallContext(CallContextKind.Explorer, 7, currentFolder, "Résultats"));
        vm.IsCustomizing = true;

        Assert.False(vm.AddCurrentFolderCommand.CanExecute(inbox));
        vm.AddCurrentFolder(inbox);

        Assert.Empty(h.Inbox.Items);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    // ----- Recall: the shortcut while the window is behind another one -----

    [Fact]
    public void OnRecalled_keeps_customize_mode_and_the_tab_and_takes_the_new_context()
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(DialogContext);
        vm.IsCustomizing = true;
        vm.ActiveTab = h.TabVm("Work");
        var saves = h.Persistence.SaveCount;
        var inbox = h.GroupVm("Inbox");
        vm.Selected = inbox;

        vm.OnRecalled(ExplorerContext);

        Assert.True(vm.IsCustomizing);
        Assert.Same(h.TabVm("Work"), vm.ActiveTab);
        Assert.Same(inbox, vm.Selected);
        Assert.Same(ExplorerContext, vm.Context);
        Assert.Equal("Called from File Explorer", vm.ContextLine);
        Assert.Equal(saves, h.Persistence.SaveCount);

        // « + Current folder » now adds the folder of the new calling window.
        Assert.True(vm.AddCurrentFolderCommand.CanExecute(inbox));
        vm.AddCurrentFolder(inbox);
        Assert.Equal(@"C:\Users\Me\Work", Assert.Single(h.Inbox.Items).Path);
    }

    [Fact]
    public void OnRecalled_ends_a_rename()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var projects = h.GroupVm("Projects");
        vm.BeginRename(projects);

        vm.OnRecalled(ExplorerContext);

        Assert.False(projects.IsRenaming);
        Assert.True(vm.IsCustomizing);
    }

    [Fact]
    public async Task OnRecalled_drops_an_activation_outcome_of_the_previous_call()
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(ExplorerContext);
        var pending = new TaskCompletionSource<ActivationOutcome>();
        h.Activator.Pending = pending.Task;

        var click = vm.ActivateItemAsync(h.ItemVm("Music"), ctrl: false);
        vm.OnRecalled(DialogContext);
        pending.SetResult(ActivationOutcome.Failed);
        await click;

        Assert.Empty(h.Prompts.Errors);
        Assert.Equal(0, h.Host.HideCount);
    }

    [Fact]
    public void RemoveItem_removes_the_view_model()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var media = h.GroupVm("Media");
        var changes = RecordChanges(media);

        vm.RemoveItem(h.ItemVm("Music"));

        Assert.Equal(["Notes.txt"], media.Items.Select(i => i.DisplayName));
        Assert.Equal([h.Notes], h.Media.Items);
        Assert.Equal(1, media.Count);
        Assert.Contains(nameof(GroupViewModel.Count), changes);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void MoveItem_moves_the_view_model_like_the_model()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var acme = h.ItemVm("Acme");
        var media = h.GroupVm("Media");
        var projects = h.GroupVm("Projects");

        vm.MoveItem(acme, media, 1);

        Assert.Equal(["Music", "Acme", "Notes.txt"], media.Items.Select(i => i.DisplayName));
        Assert.Equal([h.Music, h.Acme, h.Notes], h.Media.Items);
        Assert.Equal(["Desktop"], projects.Items.Select(i => i.DisplayName));
        Assert.Same(media, acme.Group);
        Assert.Equal(3, media.Count);
        Assert.Equal(1, projects.Count);

        vm.MoveItem(h.ItemVm("Notes.txt"), media, 0);

        Assert.Equal(["Notes.txt", "Music", "Acme"], media.Items.Select(i => i.DisplayName));
        Assert.Equal([h.Notes, h.Music, h.Acme], h.Media.Items);
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void Moved_groups_and_items_already_have_their_new_owner_when_inserted()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var projects = h.GroupVm("Projects");
        var right = h.ColumnVm("Work", 1);
        var acme = h.ItemVm("Acme");
        var media = h.GroupVm("Media");
        ColumnViewModel? columnSeen = null;
        GroupViewModel? groupSeen = null;
        right.Groups.CollectionChanged += (_, e) => columnSeen = ((GroupViewModel)e.NewItems![0]!).Column;
        media.Items.CollectionChanged += (_, e) => groupSeen = ((ItemViewModel)e.NewItems![0]!).Group;

        vm.MoveGroup(projects, right, 0);
        vm.MoveItem(acme, media, 0);

        Assert.Same(right, columnSeen);
        Assert.Same(media, groupSeen);
    }

    [Fact]
    public void SpecialFolderStates_marks_present_items()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var projects = h.GroupVm("Projects");
        var changes = RecordChanges(projects);

        var states = projects.SpecialFolderStates;

        Assert.Equal(SpecialFolderCatalog.All, states.Select(s => s.Info));
        Assert.Equal([SpecialFolderKey.Desktop], states.Where(s => s.AlreadyPresent).Select(s => s.Info.Key));
        Assert.Equal("Desktop", states.Single(s => s.Info.Key == SpecialFolderKey.Desktop).Name);

        vm.AddSpecialFolder(projects, SpecialFolderKey.Documents);

        Assert.Contains(nameof(GroupViewModel.SpecialFolderStates), changes);
        Assert.Equal(
            [SpecialFolderKey.Desktop, SpecialFolderKey.Documents],
            projects.SpecialFolderStates.Where(s => s.AlreadyPresent).Select(s => s.Info.Key));

        vm.RemoveItem(h.ItemVm("Desktop"));

        Assert.Equal(
            [SpecialFolderKey.Documents],
            projects.SpecialFolderStates.Where(s => s.AlreadyPresent).Select(s => s.Info.Key));
        Assert.DoesNotContain(h.GroupVm("Inbox").SpecialFolderStates, s => s.AlreadyPresent);
    }

    [Fact]
    public void SpecialFolderStates_follow_items_moved_in_and_out()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var inbox = h.GroupVm("Inbox");
        var projects = h.GroupVm("Projects");
        _ = inbox.SpecialFolderStates;
        _ = projects.SpecialFolderStates;

        vm.MoveItem(h.ItemVm("Desktop"), inbox, 0);

        Assert.True(inbox.SpecialFolderStates.Single(s => s.Info.Key == SpecialFolderKey.Desktop).AlreadyPresent);
        Assert.DoesNotContain(projects.SpecialFolderStates, s => s.AlreadyPresent);
    }

    // ----- Rename and selection -----

    [Fact]
    public void F2_renames_selected_and_blank_name_is_refused()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var projects = h.GroupVm("Projects");
        var changes = RecordChanges(projects);
        vm.Selected = projects;

        vm.RenameSelectedCommand.Execute(null);
        Assert.True(projects.IsRenaming);

        vm.CommitRename(projects, "   ");

        Assert.False(projects.IsRenaming);
        Assert.Equal("Projects", projects.Name);
        Assert.Equal("Projects", h.Projects.Name);
        Assert.Equal(0, h.Persistence.SaveCount);

        vm.RenameSelected();
        vm.CommitRename(projects, "  Clients ");

        Assert.False(projects.IsRenaming);
        Assert.Equal("Clients", projects.Name);
        Assert.Equal("Clients", h.Projects.Name);
        Assert.Contains(nameof(GroupViewModel.Name), changes);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void F2_does_nothing_in_normal_mode_or_without_a_selection()
    {
        var h = new Harness();
        var vm = h.Vm;
        var media = h.GroupVm("Media");
        vm.Selected = media;

        vm.RenameSelected();
        Assert.False(media.IsRenaming);

        vm.IsCustomizing = true;
        vm.Selected = null;
        vm.RenameSelected();
        vm.Selected = "not a board object";
        vm.RenameSelected();

        Assert.False(media.IsRenaming);
    }

    [Fact]
    public void Renaming_a_tab_or_an_item_updates_its_name()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var work = h.TabVm("Work");
        var acme = h.ItemVm("Acme");
        var tabChanges = RecordChanges(work);
        var itemChanges = RecordChanges(acme);

        vm.BeginRename(work);
        vm.CommitRename(work, "Office");
        vm.BeginRename(acme);
        vm.CommitRename(acme, "Client A");

        Assert.Equal("Office", work.Name);
        Assert.Equal("Office", h.Work.Name);
        Assert.Contains(nameof(TabViewModel.Name), tabChanges);
        Assert.Equal("Client A", acme.DisplayName);
        Assert.Equal("Client A", h.Acme.Name);
        Assert.Equal(@"C:\Projects\Acme", h.Acme.Path);
        Assert.Contains(nameof(ItemViewModel.DisplayName), itemChanges);
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void Only_one_object_is_renamed_at_a_time()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var work = h.TabVm("Work");
        var inbox = h.GroupVm("Inbox");

        vm.BeginRename(work);
        vm.BeginRename(inbox);

        Assert.False(work.IsRenaming);
        Assert.True(inbox.IsRenaming);
        Assert.Same(inbox, vm.Selected);

        vm.CommitRename(work, "Ignored");
        Assert.Equal("Work", h.Work.Name);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void CancelRename_keeps_the_name_and_saves_nothing()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var inbox = h.GroupVm("Inbox");
        vm.BeginRename(inbox);

        vm.CancelRenameCommand.Execute(inbox);

        Assert.False(inbox.IsRenaming);
        Assert.Equal("Inbox", h.Inbox.Name);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void Committing_the_default_name_unchanged_keeps_it_translatable()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var desktop = h.ItemVm("Desktop");

        vm.BeginRename(desktop);
        vm.CommitRename(desktop, "Desktop");

        Assert.False(desktop.IsRenaming);
        Assert.Null(h.Desktop.Name);
        Assert.Equal(0, h.Persistence.SaveCount);

        h.Localizer.SetLanguage("fr");

        Assert.Equal("Bureau", desktop.DisplayName);
    }

    [Fact]
    public void Committing_an_unchanged_name_saves_nothing()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var work = h.TabVm("Work");
        var inbox = h.GroupVm("Inbox");
        var acme = h.ItemVm("Acme");

        vm.BeginRename(work);
        vm.CommitRename(work, "Work");
        vm.BeginRename(inbox);
        vm.CommitRename(inbox, "  Inbox ");
        vm.BeginRename(acme);
        vm.CommitRename(acme, "Acme");

        Assert.False(work.IsRenaming || inbox.IsRenaming || acme.IsRenaming);
        Assert.Null(h.Acme.Name);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void Switching_tab_clears_a_selection_left_on_the_hidden_tab()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var media = h.GroupVm("Media");
        vm.Selected = media;

        vm.NextTab();
        vm.RenameSelected();

        Assert.Null(vm.Selected);
        Assert.False(media.IsRenaming);

        vm.Selected = h.ItemVm("Acme");
        vm.ActiveTab = h.TabVm("Home");
        Assert.Null(vm.Selected);

        var work = h.TabVm("Work");
        vm.Selected = work;
        vm.NextTab();
        Assert.Same(work, vm.Selected);

        vm.Selected = h.GroupVm("Projects");
        vm.ActiveTab = work;
        Assert.Same(h.GroupVm("Projects"), vm.Selected);
    }

    [Fact]
    public void Switching_tab_cancels_a_rename()
    {
        var h = new Harness();
        var vm = h.Customizing();
        var media = h.GroupVm("Media");
        vm.BeginRename(media);

        vm.NextTab();

        Assert.False(media.IsRenaming);
    }

    [Fact]
    public void Removing_the_selected_object_clears_the_selection()
    {
        var h = new Harness();
        var vm = h.Customizing();
        vm.Selected = h.ItemVm("Acme");

        vm.RemoveGroup(h.GroupVm("Projects"));

        Assert.Null(vm.Selected);
    }

    // ----- Language -----

    [Fact]
    public void LanguageChanged_refreshes_context_line_labels_and_display_names()
    {
        var h = new Harness();
        var vm = h.Vm;
        vm.OnShown(ExplorerContext);
        var desktop = h.ItemVm("Desktop");
        var acme = h.ItemVm("Acme");
        var projects = h.GroupVm("Projects");
        _ = projects.SpecialFolderStates;
        var vmChanges = RecordChanges(vm);
        var itemChanges = RecordChanges(desktop);
        var groupChanges = RecordChanges(projects);

        h.Localizer.SetLanguage("fr");

        Assert.Equal("Appelé depuis l'Explorateur", vm.ContextLine);
        Assert.Equal("Configurer", vm.CustomizeLabel);
        Assert.Equal("Bureau", desktop.DisplayName);
        Assert.Equal("Acme", acme.DisplayName);
        Assert.Equal("Bureau", projects.SpecialFolderStates.Single(s => s.Info.Key == SpecialFolderKey.Desktop).Name);
        Assert.Contains(nameof(MainViewModel.ContextLine), vmChanges);
        Assert.Contains(nameof(MainViewModel.CustomizeLabel), vmChanges);
        Assert.Contains(nameof(ItemViewModel.DisplayName), itemChanges);
        Assert.Contains(nameof(GroupViewModel.SpecialFolderStates), groupChanges);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void New_tabs_and_groups_are_named_in_the_current_language()
    {
        var h = new Harness();
        var vm = h.Customizing();
        h.Localizer.SetLanguage("fr");

        vm.AddTab();
        vm.AddGroup(vm.ActiveTab.Columns[0]);

        Assert.Equal("Nouvel onglet", vm.ActiveTab.Name);
        Assert.Equal("Nouveau groupe", vm.ActiveTab.Columns[0].Groups[0].Name);
    }
}
