// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Web;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

/// <summary>The Add menu of a group: its first line (current folder or page), its footer, its delegations.</summary>
public sealed class AddMenuViewModelTests
{
    private const string Folder = @"C:\Work\Clients";
    private const string PageUrl = "https://example.com/docs";
    private static readonly nint BrowserWindow = 4242;
    private static readonly nint ExplorerWindow = 2424;

    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(200);

    // ----- Context: Explorer, file dialog, other -----

    [Fact]
    public void Explorer_context_offers_current_folder_with_footer()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(AddMenuCurrent.Folder, menu.CurrentKind);
        Assert.Equal(Folder, menu.CurrentDetail);
        Assert.False(menu.CurrentAlreadyPresent);
        Assert.Equal(h.Localizer["AddMenu.FromExplorer"], menu.Footer);
        Assert.Equal(0, h.Browser.Calls);
    }

    [Fact]
    public void File_dialog_context_offers_current_folder()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.FileDialog, 1, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(AddMenuCurrent.Folder, menu.CurrentKind);
        Assert.Equal(Folder, menu.CurrentDetail);
        Assert.Equal(h.Localizer["AddMenu.FromFileDialog"], menu.Footer);
    }

    [Fact]
    public void Other_context_offers_nothing()
    {
        var h = new Harness();
        Show(h, CallContext.None);

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Null(menu.CurrentDetail);
        Assert.Null(menu.Footer);
        Assert.Equal(0, h.Browser.Calls);
    }

    [Fact]
    public void Explorer_search_results_offer_no_folder_but_keep_the_footer()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, "search-ms:query=a"));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(h.Localizer["AddMenu.FromExplorer"], menu.Footer);
    }

    [Fact]
    public void Virtual_folder_is_shown_by_its_shell_name()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, "::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}", "Home"));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(AddMenuCurrent.Folder, menu.CurrentKind);
        Assert.Equal("Home", menu.CurrentDetail);
    }

    [Fact]
    public void Current_folder_already_in_group_is_marked_and_not_added_twice()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, @"c:\projects\acme\"));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Projects"));
        menu.AddCurrent();

        Assert.True(menu.CurrentAlreadyPresent);
        Assert.Equal(2, h.Projects.Items.Count);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddCurrent_adds_the_current_folder()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        menu.AddCurrent();

        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal(ItemKind.Folder, item.Kind);
        Assert.Equal(Folder, item.Path);
        Assert.Single(h.GroupVm("Inbox").Items);
    }

    // ----- Context: Explorer or file dialog whose folder the capture could not read -----

    [Fact]
    public void Folder_already_captured_is_not_read_again()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Settle(ui);

        Assert.False(menu.IsReading);
        Assert.Equal(0, h.Folders.Calls);
    }

    [Theory]
    [InlineData(CallContextKind.Explorer)]
    [InlineData(CallContextKind.FileDialog)]
    public void Folder_missing_from_the_capture_is_read_when_the_menu_opens(CallContextKind kind)
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.Responder = call => call < 2 ? null : new FolderLocation(Folder, null);
        Show(h, new CallContext(kind, ExplorerWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        // Nothing is known when the menu opens: the first line comes once read.
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.True(menu.IsReading);

        NextFolderAttempt(ui, h);
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Folder);

        Assert.Equal(Folder, menu.CurrentDetail);
        Assert.False(menu.CurrentAlreadyPresent);
        Assert.False(menu.IsReading);
        Assert.Equal(2, h.Folders.Calls);
        Assert.Equal(kind, h.Folders.LastKind);
        Assert.Equal(ExplorerWindow, h.Folders.LastWindow);

        // The read stops at the first success.
        h.Time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(2, h.Folders.Calls);
    }

    [Fact]
    public void Folder_read_late_completes_the_context_and_can_be_added()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.Responder = _ => new FolderLocation("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}", "Home");
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Folder);
        menu.AddCurrent();

        Assert.Equal("Home", menu.CurrentDetail);
        Assert.Equal("Home", h.Vm.Context.CurrentFolderName);
        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal("::{F874310E-B6B7-47DC-BC84-B9E6B38F5903}", item.Path);
        Assert.Equal("Home", item.Name);
    }

    [Fact]
    public void Folder_read_late_already_in_group_is_marked()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.Responder = _ => new FolderLocation(@"c:\projects\acme\", null);
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Projects"));
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Folder);

        Assert.True(menu.CurrentAlreadyPresent);
    }

    [Fact]
    public void Folder_read_gives_up_after_two_seconds()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        for (var i = 0; i < 9; i++)
        {
            NextFolderAttempt(ui, h);
        }

        ui.WaitUntil(() => h.Time.PendingTimers == 1);
        h.Time.Advance(Step);
        ui.WaitUntil(() => !menu.IsReading);

        Assert.Equal(10, h.Folders.Calls);
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Null(h.Vm.Context.CurrentFolder);
        Assert.Equal(h.Localizer["AddMenu.FromExplorer"], menu.Footer);
    }

    [Fact]
    public void Folder_read_late_does_not_touch_a_newer_context()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.Responder = call => call < 2 ? null : new FolderLocation(Folder, null);
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Folders.Calls == 1 && h.Time.PendingTimers == 1);

        // A new call ends the menu's read: the folder of the old call never reaches the new context.
        var newer = new CallContext(CallContextKind.Explorer, 77, null);
        h.Vm.OnRecalled(newer);
        h.Time.Advance(Step);
        Settle(ui);

        Assert.Same(newer, h.Vm.Context);
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Folders.Calls);
    }

    [Fact]
    public void Search_results_are_not_read_again()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, "search-ms:query=a"));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Settle(ui);

        Assert.False(menu.IsReading);
        Assert.Equal(0, h.Folders.Calls);
    }

    [Fact]
    public void Folder_read_late_as_search_results_offers_nothing()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.Responder = _ => new FolderLocation("search-ms:query=a", null);
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => !menu.IsReading);
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Folders.Calls);
    }

    [Fact]
    public void Folder_is_not_read_without_a_window()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 0, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Settle(ui);

        Assert.False(menu.IsReading);
        Assert.Equal(0, h.Folders.Calls);
    }

    // ----- Context: browser -----

    [Fact]
    public void Browser_context_shows_url_once_read()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 3 ? null : new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        // Nothing is known when the menu opens: the footer is there, the first line is not.
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(h.Localizer.Format("AddMenu.FromBrowser", "Firefox"), menu.Footer);

        // The attempts at 0 ms and 200 ms answer null; the one at 400 ms, inside the first 600 ms, answers the page.
        NextAttempt(ui, h);
        NextAttempt(ui, h);
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);

        Assert.Equal(PageUrl, menu.CurrentDetail);
        Assert.False(menu.CurrentAlreadyPresent);
        Assert.Equal(3, h.Browser.Calls);
        Assert.Equal(BrowserWindow, h.Browser.LastWindow);
        Assert.Equal(BrowserKind.Firefox, h.Browser.LastBrowser);
        Assert.Equal(h.Localizer.Format("AddMenu.FromBrowser", "Firefox"), menu.Footer);
        Assert.False(menu.IsReading);

        // The read stops at the first success: nothing more is called.
        h.Time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(3, h.Browser.Calls);
    }

    [Theory]
    [InlineData(BrowserKind.Firefox, "Firefox")]
    [InlineData(BrowserKind.Chrome, "Chrome")]
    [InlineData(BrowserKind.Edge, "Edge")]
    public void Browser_footer_names_the_browser(BrowserKind browser, string name)
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        Show(h, BrowserContext(browser));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(h.Localizer.Format("AddMenu.FromBrowser", name), menu.Footer);
    }

    [Fact]
    public void Browser_context_without_a_known_browser_offers_nothing()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Browser, BrowserWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Null(menu.Footer);
        Assert.False(menu.IsReading);
        Assert.Equal(0, h.Browser.Calls);
    }

    [Fact]
    public void Browser_context_gives_up_after_two_seconds()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        Show(h, BrowserContext(BrowserKind.Chrome));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Assert.True(menu.IsReading);

        // One attempt every 200 ms from 0 to 1800 ms (the first at once, nine more as time passes); at 2 s the read is over.
        for (var i = 0; i < 9; i++)
        {
            NextAttempt(ui, h);
        }

        ui.WaitUntil(() => h.Time.PendingTimers == 1);
        h.Time.Advance(Step);

        ui.WaitUntil(() => !menu.IsReading);
        var calls = h.Browser.Calls;
        Assert.Equal(10, calls);
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Null(menu.CurrentDetail);
        Assert.Equal(h.Localizer.Format("AddMenu.FromBrowser", "Chrome"), menu.Footer);

        h.Time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(0, h.Time.PendingTimers);
        Assert.Equal(calls, h.Browser.Calls);
    }

    [Fact]
    public void Disposed_menu_publishes_nothing()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 2 ? null : new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Edge));

        var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1 && h.Time.PendingTimers == 1);

        menu.Dispose();
        h.Time.Advance(Step);
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Null(menu.CurrentDetail);
        Assert.Equal(1, h.Browser.Calls);
        Assert.False(menu.IsReading);
    }

    [Fact]
    public void Menu_disposed_while_an_attempt_is_under_way_publishes_nothing()
    {
        using var ui = new ManualSynchronizationContext();
        using var gate = new ManualResetEventSlim();
        var h = new Harness();
        h.Browser.Gate = gate;
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Assert.True(h.Browser.Started.Wait(TimeSpan.FromSeconds(5)));

        menu.Dispose();
        gate.Set();
        h.Time.Advance(TimeSpan.FromSeconds(5));

        // The attempt in flight ends with a page: nothing is published, and nothing is attempted again.
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Browser.Calls);
    }

    [Fact]
    public void Result_is_published_on_the_context_captured_when_the_menu_opens()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Assert.True(SpinWait.SpinUntil(() => h.Browser.Calls == 1, TimeSpan.FromSeconds(5)));
        Thread.Sleep(50);

        // Posted, not applied: the page shows only when the owner of the context runs it.
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);
    }

    [Fact]
    public void Current_url_already_in_group_is_marked()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness(arrange: x => x.Inbox.Items.Add(new Item { Kind = ItemKind.Url, Url = "https://example.com/docs" }));
        h.Browser.Responder = _ => new BrowserPage("HTTPS://Example.com/docs", "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);
        menu.AddCurrent();

        Assert.True(menu.CurrentAlreadyPresent);
        Assert.Single(h.Inbox.Items);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddCurrent_adds_the_page_named_by_its_title()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Prompts.AcceptsUrlPrefill = true;
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);
        menu.AddCurrent();

        Assert.Equal(PageUrl, Assert.Single(h.Prompts.UrlPrefills));
        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal(ItemKind.Url, item.Kind);
        Assert.Equal(PageUrl, item.Url);
        Assert.Equal("Docs", item.Name);
    }

    [Fact]
    public void AddCurrent_does_nothing_before_the_page_is_read()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Prompts.AcceptsUrlPrefill = true;
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        menu.AddCurrent();

        Assert.Null(menu.CurrentPage);
        Assert.Empty(h.Prompts.UrlPrefills);
        Assert.Empty(h.Inbox.Items);
    }

    [Fact]
    public void CurrentPage_is_the_page_read_until_the_menu_is_disposed()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        var page = new BrowserPage(PageUrl, "Docs");
        h.Browser.Responder = _ => page;
        Show(h, BrowserContext(BrowserKind.Firefox));

        var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);

        Assert.Same(page, menu.CurrentPage);
        menu.Dispose();
        Assert.Null(menu.CurrentPage);
    }

    [Fact]
    public void CurrentPage_is_null_when_the_group_already_holds_it()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness(arrange: x => x.Inbox.Items.Add(new Item { Kind = ItemKind.Url, Url = PageUrl }));
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);

        Assert.Null(menu.CurrentPage);
    }

    // ----- The window hides, the context changes, another menu opens -----

    [Fact]
    public void Hiding_the_window_stops_the_read()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 2 ? null : new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1 && h.Time.PendingTimers == 1);

        h.Vm.Close();
        h.Time.Advance(Step);
        Settle(ui);

        Assert.Equal(1, h.Host.HideCount);
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Browser.Calls);
    }

    [Fact]
    public void Deactivated_window_that_hides_stops_the_read()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 2 ? null : new BrowserPage(PageUrl, "Docs");
        h.Vm.OnShown(BrowserContext(BrowserKind.Firefox)); // Normal use: losing the focus hides the window.

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1 && h.Time.PendingTimers == 1);

        h.Vm.OnDeactivated();
        h.Time.Advance(Step);
        Settle(ui);

        Assert.Equal(1, h.Host.HideCount);
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Browser.Calls);
    }

    [Fact]
    public void Hidden_window_stops_the_read_whatever_the_path_of_the_hide()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 2 ? null : new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1 && h.Time.PendingTimers == 1);

        // The shortcut hides the window without going through the view model's commands.
        h.Vm.OnHidden();
        h.Time.Advance(Step);
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Browser.Calls);
    }

    [Fact]
    public void Page_read_after_the_deadline_is_not_shown()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = _ =>
        {
            h.Time.Advance(TimeSpan.FromMilliseconds(2100)); // The attempt ends after the two seconds.
            return new BrowserPage(PageUrl, "Docs");
        };
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1);
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Null(menu.CurrentDetail);
        Assert.False(menu.IsReading);
        Assert.Equal(1, h.Browser.Calls);
    }

    [Fact]
    public void Deadline_counts_from_the_opening_of_the_menu()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));
        h.Browser.Gate = new ManualResetEventSlim();

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Assert.True(h.Browser.Started.Wait(TimeSpan.FromSeconds(5)));
        h.Time.Advance(TimeSpan.FromSeconds(3)); // Passes while the first attempt is not over.
        h.Browser.Gate.Set();
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
    }

    [Fact]
    public void Browser_page_is_not_read_without_a_synchronization_context()
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var h = new Harness();
            Show(h, BrowserContext(BrowserKind.Firefox));

            using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
            Thread.Sleep(100);

            Assert.False(menu.IsReading);
            Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
            Assert.Equal(h.Localizer.Format("AddMenu.FromBrowser", "Firefox"), menu.Footer);
            Assert.Equal(0, h.Browser.Calls);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void Disposed_menu_does_nothing()
    {
        var h = new Harness();
        h.Prompts.FolderToPick = @"E:\Archive";
        h.Prompts.FileToPick = @"E:\Archive\Plan.pdf";
        h.Prompts.UrlToAsk = "https://contoso.com/";
        Show(h, new CallContext(CallContextKind.Explorer, 1, Folder));

        var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        menu.Dispose();
        menu.AddCurrent();
        menu.AddFolder();
        menu.AddFile();
        menu.AddUrl();
        menu.AddSpecialFolder(SpecialFolderKey.Documents);
        menu.AddSeparator();

        Assert.Empty(h.Inbox.Items);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void Leaving_Customize_mode_stops_the_read()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 2 ? null : new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1 && h.Time.PendingTimers == 1);

        h.Vm.IsCustomizing = false;
        h.Time.Advance(Step);
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Browser.Calls);
    }

    [Fact]
    public void New_call_of_the_window_stops_the_read_of_the_previous_menu()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 2 ? null : new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1 && h.Time.PendingTimers == 1);

        h.Vm.OnRecalled(CallContext.None);
        h.Time.Advance(Step);
        Settle(ui);

        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.Equal(1, h.Browser.Calls);
    }

    [Fact]
    public void Opening_a_menu_stops_the_read_of_the_previous_one()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = call => call < 3 ? null : new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        var first = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Browser.Calls == 1 && h.Time.PendingTimers == 1);

        using var second = h.Vm.OpenAddMenu(h.GroupVm("Projects"));
        ui.WaitUntil(() => h.Browser.Calls == 2 && h.Time.PendingTimers == 1);

        // Only the second read goes on: its next attempt answers the page, and the first read makes none.
        h.Time.Advance(Step);
        ui.WaitUntil(() => second.CurrentKind == AddMenuCurrent.Url);
        Settle(ui);

        Assert.Equal(3, h.Browser.Calls);
        Assert.Equal(AddMenuCurrent.None, first.CurrentKind);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, Folder));

        var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        menu.Dispose();
        menu.Dispose();

        Assert.Equal(AddMenuCurrent.Folder, menu.CurrentKind);
    }

    // ----- « File from current folder… » -----

    [Fact]
    public void File_from_current_folder_is_shown_with_the_current_folder()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.True(menu.ShowsCurrentFolderFile);
        Assert.True(menu.ShowsTopSection);
    }

    [Fact]
    public void File_from_current_folder_stays_available_when_the_folder_is_already_in_the_group()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, @"c:\projects\acme\"));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Projects"));

        Assert.True(menu.CurrentAlreadyPresent);
        Assert.True(menu.ShowsCurrentFolderFile);
    }

    [Theory]
    [InlineData("::{645FF040-5081-101B-9F08-00AA002F954E}")]
    [InlineData("search-ms:query=a")]
    [InlineData(null)]
    public void File_from_current_folder_is_hidden_for_the_recycle_bin_search_results_and_no_folder(string? current)
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, current));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.False(menu.ShowsCurrentFolderFile);
    }

    [Fact]
    public void Recycle_bin_keeps_its_current_folder_line_and_the_top_section()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, "::{645FF040-5081-101B-9F08-00AA002F954E}"));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.Equal(AddMenuCurrent.Folder, menu.CurrentKind);
        Assert.False(menu.ShowsCurrentFolderFile);
        Assert.True(menu.ShowsTopSection);
    }

    [Fact]
    public void File_from_current_folder_is_hidden_from_another_window()
    {
        var h = new Harness();
        Show(h, CallContext.None);

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));

        Assert.False(menu.ShowsCurrentFolderFile);
        Assert.False(menu.ShowsTopSection);
    }

    [Fact]
    public void File_from_current_folder_appears_with_the_folder_read_late()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.Responder = _ => new FolderLocation(Folder, null);
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, null));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        var changes = RecordChanges(menu);
        Assert.False(menu.ShowsCurrentFolderFile);
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Folder);

        Assert.True(menu.ShowsCurrentFolderFile);
        Assert.True(menu.ShowsTopSection);
        Assert.Contains(nameof(AddMenuViewModel.ShowsCurrentFolderFile), changes);
        Assert.Contains(nameof(AddMenuViewModel.ShowsTopSection), changes);
    }

    [Fact]
    public void Top_section_follows_the_page_of_a_browser()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        var changes = RecordChanges(menu);
        Assert.False(menu.ShowsTopSection);
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);

        Assert.True(menu.ShowsTopSection);
        Assert.False(menu.ShowsCurrentFolderFile);
        Assert.Contains(nameof(AddMenuViewModel.ShowsTopSection), changes);
    }

    // ----- « Selected file » -----

    [Fact]
    public void Selected_file_is_published_with_its_name()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = _ => new ExplorerSelection(@"E:\Archive\Plan.pdf");
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        var changes = RecordChanges(menu);
        Assert.False(menu.ShowsSelectedFile);
        ui.WaitUntil(() => menu.ShowsSelectedFile);
        ui.WaitUntil(() => !menu.IsReading);

        Assert.Equal(@"E:\Archive\Plan.pdf", menu.SelectedFilePath);
        Assert.Equal("Plan.pdf", menu.SelectedFileName);
        Assert.False(menu.SelectedFileAlreadyPresent);
        Assert.True(menu.ShowsTopSection);
        Assert.Equal(ExplorerWindow, h.Folders.LastSelectionWindow);
        Assert.Contains(nameof(AddMenuViewModel.ShowsSelectedFile), changes);
        Assert.Contains(nameof(AddMenuViewModel.SelectedFileName), changes);
        Assert.Contains(nameof(AddMenuViewModel.ShowsTopSection), changes);
    }

    [Fact]
    public void Selected_file_is_read_whether_or_not_the_folder_is_known()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = _ => new ExplorerSelection(@"E:\Archive\Plan.pdf");
        Show(h, ExplorerNoFolder);

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.ShowsSelectedFile);

        Assert.Equal("Plan.pdf", menu.SelectedFileName);
        Assert.Equal(AddMenuCurrent.None, menu.CurrentKind);
        Assert.True(menu.ShowsTopSection);
    }

    [Fact]
    public void No_file_shows_nothing_and_stops_the_reading()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = _ => ExplorerSelection.NoFile;
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Folders.SelectionCalls == 1);
        Settle(ui);
        h.Time.Advance(TimeSpan.FromSeconds(5));
        Settle(ui);

        Assert.Equal(1, h.Folders.SelectionCalls);
        Assert.False(menu.ShowsSelectedFile);
        Assert.Null(menu.SelectedFilePath);
        Assert.Null(menu.SelectedFileName);
        Assert.False(menu.IsReading);
    }

    [Fact]
    public void Failed_selection_attempts_are_retried_until_a_file_comes()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = call => call < 3 ? null : new ExplorerSelection(@"E:\Archive\Plan.pdf");
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Assert.True(menu.IsReading);
        NextSelectionAttempt(ui, h);
        Assert.False(menu.ShowsSelectedFile);
        NextSelectionAttempt(ui, h);
        ui.WaitUntil(() => menu.ShowsSelectedFile);
        ui.WaitUntil(() => !menu.IsReading);

        Assert.Equal(3, h.Folders.SelectionCalls);
        Assert.Equal("Plan.pdf", menu.SelectedFileName);
    }

    [Fact]
    public void Selection_is_not_read_after_two_seconds()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = _ => null;
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        for (var i = 0; i < 9; i++)
        {
            NextSelectionAttempt(ui, h);
        }

        ui.WaitUntil(() => h.Time.PendingTimers == 1);
        h.Time.Advance(Step);
        ui.WaitUntil(() => !menu.IsReading);

        Assert.Equal(10, h.Folders.SelectionCalls);
        Assert.False(menu.ShowsSelectedFile);
    }

    [Fact]
    public void Selection_answer_coming_after_two_seconds_is_not_shown()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        using var gate = new ManualResetEventSlim();
        h.Folders.SelectionResponder = _ =>
        {
            gate.Wait(TimeSpan.FromSeconds(5));
            return new ExplorerSelection(@"E:\Archive\Plan.pdf");
        };
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Folders.SelectionCalls == 1);
        h.Time.Advance(TimeSpan.FromSeconds(3)); // Passes while the attempt is not over.
        gate.Set();
        Settle(ui);

        Assert.False(menu.ShowsSelectedFile);
    }

    [Fact]
    public void Selection_is_not_published_after_dispose()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = call => call < 2 ? null : new ExplorerSelection(@"E:\Archive\Plan.pdf");
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => h.Folders.SelectionCalls == 1 && h.Time.PendingTimers == 1);
        menu.Dispose();
        h.Time.Advance(Step);
        Settle(ui);

        Assert.False(menu.ShowsSelectedFile);
        Assert.Equal(1, h.Folders.SelectionCalls);
    }

    [Fact]
    public void Selection_is_never_read_from_a_file_dialog_a_browser_another_window_or_without_a_window()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = _ => new ExplorerSelection(@"E:\Archive\Plan.pdf");
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");

        foreach (var context in new[]
                 {
                     new CallContext(CallContextKind.FileDialog, ExplorerWindow, Folder),
                     new CallContext(CallContextKind.FileDialog, ExplorerWindow, null),
                     BrowserContext(BrowserKind.Firefox),
                     CallContext.None,
                     new CallContext(CallContextKind.Explorer, 0, Folder),
                 })
        {
            Show(h, context);
            using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
            Settle(ui);

            Assert.False(menu.ShowsSelectedFile);
        }

        Assert.Equal(0, h.Folders.SelectionCalls);
    }

    [Fact]
    public void Selected_file_already_in_the_group_is_marked_and_not_added_again()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness(h => h.Inbox.Items.Add(new Item { Kind = ItemKind.File, Path = @"E:\Archive\Plan.pdf" }));
        h.Folders.SelectionResponder = _ => new ExplorerSelection(@"e:\archive\plan.pdf");
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.ShowsSelectedFile);
        menu.AddSelectedFile();

        Assert.True(menu.SelectedFileAlreadyPresent);
        Assert.Single(h.Inbox.Items);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddSelectedFile_adds_a_file_item_without_the_picker()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = _ => new ExplorerSelection(@"E:\Archive\Plan.pdf");
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.ShowsSelectedFile);
        menu.AddSelectedFile();

        var item = Assert.Single(h.Inbox.Items);
        Assert.Equal((ItemKind.File, @"E:\Archive\Plan.pdf"), (item.Kind, item.Path));
        Assert.Empty(h.Prompts.PickFileFolders);
        Assert.Equal(1, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddSelectedFile_does_nothing_without_a_file_or_once_disposed()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.SelectionResponder = _ => new ExplorerSelection(@"E:\Archive\Plan.pdf");
        Show(h, new CallContext(CallContextKind.Explorer, ExplorerWindow, Folder));

        var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        menu.AddSelectedFile(); // Not read yet.
        Assert.Empty(h.Inbox.Items);

        ui.WaitUntil(() => menu.ShowsSelectedFile);
        menu.Dispose();
        menu.AddSelectedFile();

        Assert.Empty(h.Inbox.Items);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void IsReading_holds_until_both_reads_end()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness();
        h.Folders.Responder = _ => new FolderLocation(Folder, null);
        h.Folders.SelectionResponder = call => call < 3 ? null : ExplorerSelection.NoFile;
        Show(h, ExplorerNoFolder);

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        Assert.True(menu.IsReading);

        // The folder read ends first: the selection one is still under way.
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Folder);
        ui.WaitUntil(() => h.Folders.SelectionCalls == 1 && h.Time.PendingTimers == 1);
        Settle(ui);
        Assert.True(menu.IsReading);

        NextSelectionAttempt(ui, h);
        Assert.True(menu.IsReading);
        NextSelectionAttempt(ui, h);
        ui.WaitUntil(() => !menu.IsReading);

        Assert.Equal(3, h.Folders.SelectionCalls);
    }

    // ----- Delegations -----

    [Fact]
    public void AddSeparator_appends_one_each_time_and_never_greys_out()
    {
        var h = new Harness();
        Show(h, new CallContext(CallContextKind.Explorer, 1, Folder));

        using (var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox")))
        {
            menu.AddSeparator();
        }

        using (var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox")))
        {
            // A group holding a separator does not hold the current folder.
            Assert.False(menu.CurrentAlreadyPresent);
            menu.AddSeparator();
        }

        Assert.Equal([ItemKind.Separator, ItemKind.Separator], h.Inbox.Items.Select(item => item.Kind));
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void Separators_never_make_a_special_folder_or_a_page_already_present()
    {
        using var ui = new ManualSynchronizationContext();
        var h = new Harness(h => h.Inbox.Items.Add(new Item { Kind = ItemKind.Separator }));
        h.Browser.Responder = _ => new BrowserPage(PageUrl, "Docs");
        Show(h, BrowserContext(BrowserKind.Firefox));

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        ui.WaitUntil(() => menu.CurrentKind == AddMenuCurrent.Url);

        Assert.False(menu.CurrentAlreadyPresent);
        Assert.All(menu.SpecialFolders, state => Assert.False(state.AlreadyPresent));
    }

    [Fact]
    public void AddFolder_AddFile_and_AddUrl_delegate_to_the_prompts()
    {
        var h = new Harness();
        h.Prompts.FolderToPick = @"E:\Archive";
        h.Prompts.FileToPick = @"E:\Archive\Plan.pdf";
        h.Prompts.UrlToAsk = "https://contoso.com/";
        Show(h, CallContext.None);

        using var menu = h.Vm.OpenAddMenu(h.GroupVm("Inbox"));
        menu.AddFolder();
        menu.AddFile();
        menu.AddUrl();

        Assert.Collection(
            h.Inbox.Items,
            item => Assert.Equal((ItemKind.Folder, @"E:\Archive"), (item.Kind, item.Path)),
            item => Assert.Equal((ItemKind.File, @"E:\Archive\Plan.pdf"), (item.Kind, item.Path)),
            item => Assert.Equal((ItemKind.Url, "https://contoso.com/"), (item.Kind, item.Url)));
    }

    /// <summary>
    /// « Folder… », « File… » and « URL… » close the menu (which disposes its view model) before their modal window
    /// opens, then ask the main view model for the group: the addition does not depend on the menu.
    /// </summary>
    [Fact]
    public void Pickers_asked_from_the_main_view_model_add_once_the_menu_is_closed()
    {
        var h = new Harness();
        h.Prompts.FolderToPick = @"E:\Archive";
        h.Prompts.FileToPick = @"E:\Archive\Plan.pdf";
        h.Prompts.UrlToAsk = "https://contoso.com/";
        Show(h, CallContext.None);
        var inbox = h.GroupVm("Inbox");

        var menu = h.Vm.OpenAddMenu(inbox);
        menu.Dispose();
        h.Vm.AddFolder(inbox);
        h.Vm.AddFile(inbox);
        h.Vm.AddUrl(inbox);

        Assert.Equal([ItemKind.Folder, ItemKind.File, ItemKind.Url], h.Inbox.Items.Select(item => item.Kind));
    }

    /// <summary>
    /// The modal window opens after the menu closed, a moment later: when Customize mode ended or the group left the
    /// board in between, nothing is asked and nothing is added.
    /// </summary>
    [Fact]
    public void Pickers_asked_after_customize_mode_ended_or_the_group_left_ask_nothing()
    {
        var h = new Harness();
        h.Prompts.FolderToPick = @"E:\Archive";
        h.Prompts.FileToPick = @"E:\Archive\Plan.pdf";
        h.Prompts.UrlToAsk = "https://contoso.com/";
        var asked = 0;
        h.Prompts.DuringPrompt = () => asked++;
        Show(h, CallContext.None);
        var inbox = h.GroupVm("Inbox");

        h.Vm.OpenAddMenu(inbox).Dispose();
        h.Vm.IsCustomizing = false;
        h.Vm.AddFolder(inbox);
        h.Vm.AddFile(inbox);
        h.Vm.AddUrl(inbox);
        Assert.Equal(0, asked);
        Assert.Empty(h.Inbox.Items);

        h.Vm.IsCustomizing = true;
        h.Vm.OpenAddMenu(inbox).Dispose();
        h.Vm.RemoveGroup(inbox);
        asked = 0;
        var saves = h.Persistence.SaveCount;
        h.Vm.AddFolder(inbox);
        h.Vm.AddFile(inbox);
        h.Vm.AddUrl(inbox);

        Assert.Equal(0, asked);
        Assert.Empty(h.Inbox.Items);
        Assert.Equal(saves, h.Persistence.SaveCount);
    }

    [Fact]
    public void AddSpecialFolder_delegates_and_special_folder_states_are_the_groups()
    {
        var h = new Harness();
        Show(h, CallContext.None);
        var group = h.GroupVm("Projects");

        using var menu = h.Vm.OpenAddMenu(group);

        Assert.Same(group.SpecialFolderStates, menu.SpecialFolders);
        Assert.True(menu.SpecialFolders.Single(state => state.Info.Key == SpecialFolderKey.Desktop).AlreadyPresent);

        menu.AddSpecialFolder(SpecialFolderKey.Documents);
        menu.AddSpecialFolder(SpecialFolderKey.Desktop);

        Assert.Equal(3, h.Projects.Items.Count);
        Assert.Equal(SpecialFolderKey.Documents, h.Projects.Items[2].Special);
    }

    private static CallContext ExplorerNoFolder => new(CallContextKind.Explorer, ExplorerWindow, null);

    private static List<string?> RecordChanges(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    /// <summary>As <see cref="NextAttempt"/>, for the read of the selected file.</summary>
    private static void NextSelectionAttempt(ManualSynchronizationContext ui, Harness h)
    {
        var before = h.Folders.SelectionCalls;
        ui.WaitUntil(() => h.Time.PendingTimers == 1);
        h.Time.Advance(Step);
        ui.WaitUntil(() => h.Folders.SelectionCalls > before);
    }

    /// <summary>The window is shown for <paramref name="context"/> and put in Customize mode, where the menu lives.</summary>
    private static void Show(Harness h, CallContext context)
    {
        h.Vm.OnShown(context);
        h.Vm.IsCustomizing = true;
    }

    private static CallContext BrowserContext(BrowserKind browser) =>
        new(CallContextKind.Browser, BrowserWindow, null, null, browser);

    /// <summary>
    /// Gives work that should NOT happen the time it would need to happen (the pool is real), pumping what is posted;
    /// the checks that follow are then about a read that is really over.
    /// </summary>
    private static void Settle(ManualSynchronizationContext ui)
    {
        var until = DateTime.UtcNow.AddMilliseconds(150);
        while (DateTime.UtcNow < until)
        {
            ui.Pump();
            Thread.Sleep(5);
        }
    }

    /// <summary>Lets the read reach its wait, then lets 200 ms pass: the next attempt runs and is waited for.</summary>
    private static void NextAttempt(ManualSynchronizationContext ui, Harness h)
    {
        var before = h.Browser.Calls;
        ui.WaitUntil(() => h.Time.PendingTimers == 1);
        h.Time.Advance(Step);
        ui.WaitUntil(() => h.Browser.Calls > before);
    }

    /// <summary>As <see cref="NextAttempt"/>, for the read of the current folder.</summary>
    private static void NextFolderAttempt(ManualSynchronizationContext ui, Harness h)
    {
        var before = h.Folders.Calls;
        ui.WaitUntil(() => h.Time.PendingTimers == 1);
        h.Time.Advance(Step);
        ui.WaitUntil(() => h.Folders.Calls > before);
    }
}
