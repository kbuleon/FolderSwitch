// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Icons;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

/// <summary>The icon picker of a tab or a group: its state at the opening, its filter and what a choice does.</summary>
public sealed class IconPickerViewModelTests
{
    private static IconPickerViewModel Open(Harness h, RenamableViewModel target) =>
        Assert.IsType<IconPickerViewModel>(h.Vm.OpenIconPicker(target));

    [Fact]
    public void Opens_on_automatic_by_default()
    {
        var h = new Harness();
        h.Customizing();

        var picker = Open(h, h.GroupVm("Projects"));

        Assert.Equal(IconChoice.Automatic, picker.Current);
        Assert.Null(picker.CurrentEmoji);
    }

    [Fact]
    public void Opens_on_none_for_a_chosen_empty_icon()
    {
        var h = new Harness(h => h.Media.IconChosen = true);
        h.Customizing();

        var picker = Open(h, h.GroupVm("Media"));

        Assert.Equal(IconChoice.None, picker.Current);
        Assert.Null(picker.CurrentEmoji);
    }

    [Fact]
    public void Opens_on_the_chosen_emoji()
    {
        var h = new Harness(h =>
        {
            h.Work.Icon = "🐱";
            h.Work.IconChosen = true;
        });
        h.Customizing();

        var picker = Open(h, h.TabVm("Work"));

        Assert.Equal(IconChoice.Emoji, picker.Current);
        Assert.Equal("🐱", picker.CurrentEmoji);
    }

    [Fact]
    public void Automatic_icon_is_the_suggestion_for_the_current_name()
    {
        var h = new Harness(h => h.Projects.Name = "Finance");
        h.Customizing();

        Assert.Equal("💵", Open(h, h.GroupVm("Finance")).AutomaticIcon);
        Assert.Null(Open(h, h.GroupVm("Inbox")).AutomaticIcon);
    }

    [Fact]
    public void Opens_nothing_for_an_item()
    {
        var h = new Harness();
        h.Customizing();

        Assert.Null(h.Vm.OpenIconPicker(h.ItemVm("Acme")));
    }

    [Fact]
    public void Without_a_filter_every_drawable_emoji_is_a_result()
    {
        var h = new Harness();
        h.Glyphs.Refused.Add("🏖");
        h.Localizer.SetLanguage("fr");
        h.Customizing();

        var picker = Open(h, h.GroupVm("Inbox"));

        Assert.False(picker.IsFiltering);
        Assert.Equal(["😺", "🐱", "💵"], picker.Results.Select(entry => entry.Emoji));

        picker.FilterText = "   ";
        Assert.False(picker.IsFiltering);
    }

    [Fact]
    public void Filter_shows_the_matching_emojis_whole_words_first()
    {
        var h = new Harness();
        h.Localizer.SetLanguage("fr");
        h.Customizing();
        var picker = Open(h, h.GroupVm("Inbox"));
        var changes = new List<string?>();
        picker.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        picker.FilterText = "chat";

        Assert.True(picker.IsFiltering);
        Assert.Equal(["🐱", "😺"], picker.Results.Select(entry => entry.Emoji));
        Assert.Contains(nameof(IconPickerViewModel.Results), changes);
        Assert.Contains(nameof(IconPickerViewModel.IsFiltering), changes);

        picker.FilterText = "";
        Assert.False(picker.IsFiltering);
        Assert.Equal(4, picker.Results.Count);
    }

    [Fact]
    public void Choose_first_result_chooses_it_and_closes()
    {
        var h = new Harness();
        h.Localizer.SetLanguage("fr");
        h.Customizing();
        var group = h.GroupVm("Inbox");
        var picker = Open(h, group);
        var closed = 0;
        picker.Closed += (_, _) => closed++;
        picker.FilterText = "chat";

        var chosen = picker.ChooseFirstResult();

        Assert.True(chosen);
        Assert.Equal("🐱", group.Icon);
        Assert.True(group.IconChosen);
        Assert.Equal(1, closed);
        Assert.Equal(IconChoice.Emoji, picker.Current);
        Assert.Equal("🐱", picker.CurrentEmoji);
        Assert.Same(h.Config, Assert.Single(h.Persistence.Saved));
    }

    [Fact]
    public void Choose_first_result_without_result_changes_nothing()
    {
        var h = new Harness();
        h.Localizer.SetLanguage("fr");
        h.Customizing();
        var group = h.GroupVm("Inbox");
        var picker = Open(h, group);
        var closed = 0;
        picker.Closed += (_, _) => closed++;
        picker.FilterText = "zzzz";

        var chosen = picker.ChooseFirstResult();

        Assert.False(chosen);
        Assert.Empty(picker.Results);
        Assert.Null(group.Icon);
        Assert.False(group.IconChosen);
        Assert.Equal(0, closed);
        Assert.Equal(0, h.Persistence.SaveCount);
    }

    [Fact]
    public void Choosing_none_and_automatic_close_the_picker_and_save()
    {
        var h = new Harness(h => h.Projects.Name = "Finance");
        h.Customizing();
        var group = h.GroupVm("Finance");
        var closed = 0;

        var picker = Open(h, group);
        picker.Closed += (_, _) => closed++;
        picker.Choose("🐱");
        Assert.Equal("🐱", group.Icon);

        picker = Open(h, group);
        picker.Closed += (_, _) => closed++;
        picker.ChooseNone();
        Assert.Null(group.Icon);
        Assert.True(group.IconChosen);
        Assert.Equal(IconChoice.None, picker.Current);

        picker = Open(h, group);
        picker.Closed += (_, _) => closed++;
        picker.ChooseAutomatic();
        Assert.Equal("💵", group.Icon);
        Assert.False(group.IconChosen);
        Assert.Equal(IconChoice.Automatic, picker.Current);

        Assert.Equal(3, closed);
        Assert.Equal(3, h.Persistence.SaveCount);
    }

    [Fact]
    public void Sections_are_one_per_non_empty_category_in_order_with_translated_titles()
    {
        var h = new Harness();
        h.Glyphs.Refused.Add("🏖");
        h.Localizer.SetLanguage("fr");
        h.Customizing();

        var picker = Open(h, h.GroupVm("Inbox"));

        Assert.Equal(
            [EmojiCategory.SmileysEmotion, EmojiCategory.AnimalsNature, EmojiCategory.Objects],
            picker.Sections.Select(section => section.Category));
        Assert.Equal(
            ["Smileys et émotions", "Animaux et nature", "Objets"],
            picker.Sections.Select(section => section.Title));
        Assert.Equal(["😺"], picker.Sections[0].Entries.Select(entry => entry.Emoji));
    }

    [Fact]
    public void Name_of_an_emoji_follows_the_language()
    {
        var h = new Harness();
        h.Localizer.SetLanguage("fr");
        h.Customizing();
        var picker = Open(h, h.GroupVm("Inbox"));

        Assert.Equal("tête de chat", picker.NameOf("🐱"));
        Assert.Equal("beach", picker.NameOf("🏖"));
        Assert.Null(picker.NameOf("🚀"));
    }
}
