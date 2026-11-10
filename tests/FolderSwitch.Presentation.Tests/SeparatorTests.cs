// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Model;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

/// <summary>A separator of a group: added, moved and removed like an item; never named, given an icon, counted or opened.</summary>
public sealed class SeparatorTests
{
    private static readonly CallContext ExplorerContext = new(CallContextKind.Explorer, 42, @"C:\Users\Me\Work");
    private static readonly CallContext DialogContext = new(CallContextKind.FileDialog, 43, @"D:\Out");

    private static Item NewSeparator() => new() { Kind = ItemKind.Separator };

    /// <summary>Media holds Music, a separator, Notes.txt.</summary>
    private static Harness WithSeparatorInMedia() => new(h => h.Media.Items.Insert(1, NewSeparator()));

    private static ItemViewModel SeparatorIn(GroupViewModel group) => group.Items.Single(item => item.IsSeparator);

    [Fact]
    public void AddSeparator_appends_at_the_end_and_saves_in_Customize_mode_only()
    {
        var h = new Harness();
        var media = h.GroupVm("Media");

        h.Vm.AddSeparator(media);
        Assert.Equal(2, media.Items.Count);
        Assert.Equal(0, h.Persistence.SaveCount);

        h.Vm.IsCustomizing = true;
        h.Vm.AddSeparator(media);
        h.Vm.AddSeparator(media);

        Assert.Equal([ItemKind.Folder, ItemKind.File, ItemKind.Separator, ItemKind.Separator], h.Media.Items.Select(item => item.Kind));
        Assert.Equal(4, media.Items.Count);
        Assert.Equal(2, h.Persistence.SaveCount);
    }

    [Fact]
    public void Count_leaves_separators_out()
    {
        var h = WithSeparatorInMedia();
        var vm = h.Customizing();
        var media = h.GroupVm("Media");
        var inbox = h.GroupVm("Inbox");

        Assert.Equal(2, media.Count);

        vm.AddSeparator(inbox);
        Assert.Equal(0, inbox.Count);
        Assert.Single(inbox.Items);

        vm.MoveItem(SeparatorIn(media), inbox, 0);
        Assert.Equal(2, media.Count);
        Assert.Equal(0, inbox.Count);
        Assert.Equal(2, inbox.Items.Count);
    }

    [Fact]
    public void A_separator_has_no_name_no_path_no_icon_and_no_address()
    {
        var h = WithSeparatorInMedia();
        var separator = SeparatorIn(h.GroupVm("Media"));

        Assert.True(separator.IsSeparator);
        Assert.False(separator.IsFolder || separator.IsFile || separator.IsUrl);
        Assert.Equal(string.Empty, separator.DisplayName);
        Assert.Equal(string.Empty, separator.SecondaryText);
        Assert.Null(separator.Extension);
        Assert.Null(separator.Url);
        Assert.Null(separator.FaviconPath);
        Assert.Empty(h.Favicons.Fetches);
    }

    [Fact]
    public void A_separator_moves_within_its_group_to_another_group_and_to_another_tab_and_is_removed()
    {
        var h = WithSeparatorInMedia();
        var vm = h.Customizing();
        var media = h.GroupVm("Media");
        var separator = SeparatorIn(media);
        var line = h.Media.Items[1];

        vm.MoveItem(separator, media, 0);
        Assert.Equal([line, h.Music, h.Notes], h.Media.Items);

        vm.MoveItem(separator, h.GroupVm("Projects"), 1);
        Assert.Equal([h.Acme, line, h.Desktop], h.Projects.Items);
        Assert.Same(h.GroupVm("Projects"), separator.Group);

        vm.RemoveItem(separator);
        Assert.Equal([h.Acme, h.Desktop], h.Projects.Items);
        Assert.DoesNotContain(separator, h.GroupVm("Projects").Items);
        Assert.Equal(3, h.Persistence.SaveCount);
    }

    [Fact]
    public void A_separator_is_never_renamed_by_double_click_F2_or_commit()
    {
        var h = WithSeparatorInMedia();
        var vm = h.Customizing();
        var separator = SeparatorIn(h.GroupVm("Media"));

        vm.BeginRename(separator);
        Assert.False(separator.IsRenaming);

        vm.Selected = separator;
        vm.RenameSelectedCommand.Execute(null);
        Assert.False(separator.IsRenaming);

        vm.CommitRename(separator, "Line");
        Assert.Null(h.Media.Items[1].Name);
        Assert.Equal(string.Empty, separator.DisplayName);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void A_separator_gets_no_icon()
    {
        var h = WithSeparatorInMedia();
        var vm = h.Customizing();
        var separator = SeparatorIn(h.GroupVm("Media"));

        vm.SetChosenIcon(separator, "🐱");
        vm.SetNoIcon(separator);
        vm.SetAutomaticIcon(separator);

        Assert.Null(vm.OpenIconPicker(separator));
        Assert.Null(vm.AutomaticIcon(separator));
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void A_separator_is_usable_only_in_Customize_mode_whatever_the_calling_window()
    {
        var h = WithSeparatorInMedia();
        var vm = h.Vm;
        var separator = SeparatorIn(h.GroupVm("Media"));

        Assert.False(separator.IsUsable);
        vm.OnShown(ExplorerContext);
        Assert.False(separator.IsUsable);
        vm.OnShown(DialogContext);
        Assert.False(separator.IsUsable);

        vm.IsCustomizing = true;
        Assert.True(separator.IsUsable);

        vm.AddSeparator(h.GroupVm("Inbox"));
        var added = SeparatorIn(h.GroupVm("Inbox"));
        Assert.True(added.IsUsable);

        vm.IsCustomizing = false;
        Assert.False(added.IsUsable);
    }

    [Fact]
    public async Task A_separator_is_never_activated_nor_offered_a_new_tab()
    {
        var h = WithSeparatorInMedia();
        var vm = h.Vm;
        h.Activator.NewTabPossible = true;
        vm.OnShown(ExplorerContext);
        var separator = SeparatorIn(h.GroupVm("Media"));

        await vm.ActivateItemAsync(separator, ctrl: false);
        await vm.ActivateItemAsync(separator, ctrl: true);
        await vm.OpenInExplorerAsync(separator);

        Assert.Empty(h.Activator.Calls);
        Assert.Empty(h.Activator.ExplorerCalls);
        Assert.False(vm.CanOpenInNewTab(separator));
        Assert.Equal(0, h.Host.HideCount);
        Assert.Empty(h.Prompts.Errors);
    }

    [Fact]
    public void Separators_never_count_as_a_special_folder_the_group_holds()
    {
        var h = new Harness(h => h.Inbox.Items.Add(NewSeparator()));
        var vm = h.Customizing();
        var inbox = h.GroupVm("Inbox");

        Assert.All(inbox.SpecialFolderStates, state => Assert.False(state.AlreadyPresent));

        vm.AddSpecialFolder(inbox, SpecialFolderKey.Desktop);
        vm.AddSpecialFolder(inbox, SpecialFolderKey.Desktop);

        Assert.Equal([ItemKind.Separator, ItemKind.Folder], h.Inbox.Items.Select(item => item.Kind));
    }
}
