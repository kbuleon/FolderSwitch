// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Editing;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Tests.Editing;

public class BoardEditorTests
{
    private const string Blue = "#3b82f6";

    private static AppConfig ConfigWithTabs(params string[] tabNames)
    {
        var config = new AppConfig();
        foreach (var name in tabNames)
        {
            BoardEditor.AddTab(config, name);
        }

        return config;
    }

    private static Item FolderItem(string path) => new() { Kind = ItemKind.Folder, Path = path };

    // ----- Tabs -----

    [Fact]
    public void AddTab_creates_two_empty_columns()
    {
        var config = new AppConfig();

        var tab = BoardEditor.AddTab(config, "Work");

        Assert.Equal(2, tab.Columns.Count);
        Assert.All(tab.Columns, column => Assert.Empty(column.Groups));
        Assert.Equal("Work", tab.Name);
        Assert.Same(tab, Assert.Single(config.Tabs));
    }

    [Fact]
    public void AddTab_appends_at_the_end()
    {
        var config = ConfigWithTabs("A", "B");

        BoardEditor.AddTab(config, "C");

        Assert.Equal(new[] { "A", "B", "C" }, config.Tabs.Select(t => t.Name));
    }

    [Fact]
    public void RemoveTab_refuses_last_tab()
    {
        var config = ConfigWithTabs("Only");
        var tabId = config.Tabs[0].Id;

        var result = BoardEditor.RemoveTab(config, tabId);

        Assert.False(result.Ok);
        Assert.Equal(EditError.LastTabCannotBeRemoved, result.Error);
        Assert.Single(config.Tabs);
    }

    [Fact]
    public void RemoveTab_removes_the_tab_when_others_remain()
    {
        var config = ConfigWithTabs("A", "B");
        var tabId = config.Tabs[0].Id;

        var result = BoardEditor.RemoveTab(config, tabId);

        Assert.True(result.Ok);
        Assert.Equal(EditError.None, result.Error);
        Assert.Equal("B", Assert.Single(config.Tabs).Name);
    }

    [Fact]
    public void MoveTab_reorders()
    {
        var config = ConfigWithTabs("A", "B", "C");
        var c = config.Tabs[2];

        var result = BoardEditor.MoveTab(config, c.Id, 0);

        Assert.True(result.Ok);
        Assert.Equal(new[] { "C", "A", "B" }, config.Tabs.Select(t => t.Name));
    }

    [Fact]
    public void MoveTab_clamps_out_of_range_indexes()
    {
        var config = ConfigWithTabs("A", "B", "C");

        BoardEditor.MoveTab(config, config.Tabs[0].Id, 99);
        Assert.Equal(new[] { "B", "C", "A" }, config.Tabs.Select(t => t.Name));

        BoardEditor.MoveTab(config, config.Tabs[2].Id, -5);
        Assert.Equal(new[] { "A", "B", "C" }, config.Tabs.Select(t => t.Name));
    }

    // ----- Columns -----

    [Fact]
    public void AddColumn_appends_an_empty_auto_width_column()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];

        var column = BoardEditor.AddColumn(tab);

        Assert.Equal(3, tab.Columns.Count);
        Assert.Same(column, tab.Columns[2]);
        Assert.Empty(column.Groups);
        Assert.Null(column.Width);
    }

    [Fact]
    public void RemoveColumn_refuses_non_empty_column()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        var column = tab.Columns[0];
        BoardEditor.AddGroup(column, "Clients", Blue);

        var result = BoardEditor.RemoveColumn(tab, column.Id);

        Assert.Equal(EditError.ColumnNotEmpty, result.Error);
        Assert.Equal(2, tab.Columns.Count);
    }

    [Fact]
    public void RemoveColumn_removes_an_empty_column()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        var column = tab.Columns[0];

        var result = BoardEditor.RemoveColumn(tab, column.Id);

        Assert.True(result.Ok);
        Assert.Single(tab.Columns);
        Assert.DoesNotContain(column, tab.Columns);
    }

    [Fact]
    public void RemoveColumn_refuses_the_last_column_of_a_tab()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        BoardEditor.RemoveColumn(tab, tab.Columns[0].Id);
        var last = tab.Columns[0];

        var result = BoardEditor.RemoveColumn(tab, last.Id);

        Assert.Equal(EditError.LastColumnCannotBeRemoved, result.Error);
        Assert.Same(last, Assert.Single(tab.Columns));
    }

    [Theory]
    [InlineData(100, 150)]
    [InlineData(150, 150)]
    [InlineData(154, 150)]
    [InlineData(155, 160)]
    [InlineData(333, 330)]
    [InlineData(520, 520)]
    [InlineData(900, 520)]
    public void ClampColumnWidth_rounds_to_tens_and_clamps(int requested, int expected)
    {
        Assert.Equal(expected, BoardEditor.ClampColumnWidth(requested));
    }

    [Theory]
    [InlineData(int.MinValue, 150)]
    [InlineData(-1, 150)]
    [InlineData(int.MaxValue, 520)]
    public void ClampColumnWidth_survives_extreme_values(int requested, int expected)
    {
        Assert.Equal(expected, BoardEditor.ClampColumnWidth(requested));
    }

    [Fact]
    public void SetColumnWidth_null_means_auto()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        var column = tab.Columns[0];
        BoardEditor.SetColumnWidth(tab, column.Id, 300);

        var result = BoardEditor.SetColumnWidth(tab, column.Id, null);

        Assert.True(result.Ok);
        Assert.Null(column.Width);
    }

    [Fact]
    public void SetColumnWidth_stores_the_clamped_value()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        var column = tab.Columns[0];

        BoardEditor.SetColumnWidth(tab, column.Id, 155);
        Assert.Equal(160, column.Width);

        BoardEditor.SetColumnWidth(tab, column.Id, 900);
        Assert.Equal(520, column.Width);
    }

    [Fact]
    public void MoveColumn_reorders_with_its_groups_and_width()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        var third = BoardEditor.AddColumn(tab);
        var group = BoardEditor.AddGroup(third, "Clients", Blue);
        BoardEditor.SetColumnWidth(tab, third.Id, 300);
        var (first, second) = (tab.Columns[0], tab.Columns[1]);

        var result = BoardEditor.MoveColumn(config, third.Id, tab.Id, 0);

        Assert.True(result.Ok);
        Assert.Equal(new[] { third, first, second }, tab.Columns);
        Assert.Same(group, Assert.Single(third.Groups));
        Assert.Equal(300, third.Width);
    }

    [Fact]
    public void MoveColumn_clamps_out_of_range_indexes()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        BoardEditor.AddColumn(tab);
        var (a, b, c) = (tab.Columns[0], tab.Columns[1], tab.Columns[2]);

        BoardEditor.MoveColumn(config, a.Id, tab.Id, 99);
        Assert.Equal(new[] { b, c, a }, tab.Columns);

        BoardEditor.MoveColumn(config, a.Id, tab.Id, -5);
        Assert.Equal(new[] { a, b, c }, tab.Columns);
    }

    [Fact]
    public void MoveColumn_to_another_tab_keeps_its_groups_and_width()
    {
        var config = ConfigWithTabs("A", "B");
        var (source, target) = (config.Tabs[0], config.Tabs[1]);
        var moved = source.Columns[0];
        var group = BoardEditor.AddGroup(moved, "Clients", Blue);
        BoardEditor.SetColumnWidth(source, moved.Id, 300);
        var (first, second) = (target.Columns[0], target.Columns[1]);

        var result = BoardEditor.MoveColumn(config, moved.Id, target.Id, 1);

        Assert.True(result.Ok);
        Assert.DoesNotContain(moved, source.Columns);
        Assert.Single(source.Columns);
        Assert.Equal(new[] { first, moved, second }, target.Columns);
        Assert.Same(group, Assert.Single(moved.Groups));
        Assert.Equal(300, moved.Width);
    }

    [Fact]
    public void MoveColumn_to_another_tab_clamps_the_index_in_the_target_tab()
    {
        var config = ConfigWithTabs("A", "B");
        var (source, target) = (config.Tabs[0], config.Tabs[1]);
        var moved = source.Columns[0];

        BoardEditor.MoveColumn(config, moved.Id, target.Id, 99);

        Assert.Same(moved, target.Columns[^1]);
        Assert.Equal(3, target.Columns.Count);
    }

    [Fact]
    public void MoveColumn_refuses_to_take_the_last_column_out_of_its_tab()
    {
        var config = ConfigWithTabs("A", "B");
        var (source, target) = (config.Tabs[0], config.Tabs[1]);
        BoardEditor.RemoveColumn(source, source.Columns[0].Id);
        var last = source.Columns[0];

        var result = BoardEditor.MoveColumn(config, last.Id, target.Id, 0);

        Assert.Equal(EditError.LastColumnCannotBeRemoved, result.Error);
        Assert.Same(last, Assert.Single(source.Columns));
        Assert.Equal(2, target.Columns.Count);
    }

    [Fact]
    public void MoveColumn_reports_an_unknown_target_tab()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        var column = tab.Columns[0];

        var result = BoardEditor.MoveColumn(config, column.Id, Guid.NewGuid(), 0);

        Assert.Equal(EditError.NotFound, result.Error);
        Assert.Same(column, tab.Columns[0]);
    }

    // ----- Groups -----

    [Fact]
    public void AddGroup_appends_with_name_and_color()
    {
        var config = ConfigWithTabs("A");
        var column = config.Tabs[0].Columns[0];

        var first = BoardEditor.AddGroup(column, "One", Blue);
        var second = BoardEditor.AddGroup(column, "Two", "#22c55e");

        Assert.Equal(new[] { first, second }, column.Groups);
        Assert.Equal("Two", second.Name);
        Assert.Equal("#22c55e", second.Color);
        Assert.False(second.Collapsed);
        Assert.Empty(second.Items);
    }

    [Fact]
    public void RemoveGroup_removes_it_from_its_column()
    {
        var config = ConfigWithTabs("A");
        var column = config.Tabs[0].Columns[1];
        var group = BoardEditor.AddGroup(column, "One", Blue);

        var result = BoardEditor.RemoveGroup(config, group.Id);

        Assert.True(result.Ok);
        Assert.Empty(column.Groups);
    }

    [Fact]
    public void MoveGroup_across_columns_same_tab()
    {
        var config = ConfigWithTabs("A");
        var source = config.Tabs[0].Columns[0];
        var target = config.Tabs[0].Columns[1];
        var g1 = BoardEditor.AddGroup(source, "G1", Blue);
        var g2 = BoardEditor.AddGroup(source, "G2", Blue);
        var g3 = BoardEditor.AddGroup(target, "G3", Blue);

        var result = BoardEditor.MoveGroup(config, g1.Id, target.Id, 0);

        Assert.True(result.Ok);
        Assert.Equal(new[] { g2 }, source.Groups);
        Assert.Equal(new[] { g1, g3 }, target.Groups);
    }

    [Fact]
    public void MoveGroup_within_the_same_column_uses_the_index_in_the_final_list()
    {
        var config = ConfigWithTabs("A");
        var column = config.Tabs[0].Columns[0];
        var a = BoardEditor.AddGroup(column, "A", Blue);
        var b = BoardEditor.AddGroup(column, "B", Blue);
        var c = BoardEditor.AddGroup(column, "C", Blue);

        var result = BoardEditor.MoveGroup(config, a.Id, column.Id, 2);

        Assert.True(result.Ok);
        Assert.Equal(new[] { b, c, a }, column.Groups);
    }

    [Fact]
    public void MoveGroup_clamps_out_of_range_indexes()
    {
        var config = ConfigWithTabs("A");
        var source = config.Tabs[0].Columns[0];
        var target = config.Tabs[0].Columns[1];
        var moved = BoardEditor.AddGroup(source, "Moved", Blue);
        var existing = BoardEditor.AddGroup(target, "Existing", Blue);

        BoardEditor.MoveGroup(config, moved.Id, target.Id, 99);
        Assert.Equal(new[] { existing, moved }, target.Groups);

        BoardEditor.MoveGroup(config, moved.Id, target.Id, -3);
        Assert.Equal(new[] { moved, existing }, target.Groups);
    }

    [Fact]
    public void MoveGroup_to_another_tab_keeps_the_group_and_its_items()
    {
        var config = ConfigWithTabs("A", "B");
        var sourceColumn = config.Tabs[0].Columns[0];
        var foreignColumn = config.Tabs[1].Columns[1];
        var existing = BoardEditor.AddGroup(foreignColumn, "Other", Blue);
        var group = BoardEditor.AddGroup(sourceColumn, "G", Blue);
        var item = BoardEditor.AddItem(group, FolderItem(@"C:\Work"));
        BoardEditor.SetCollapsed(config, group.Id, true);

        var result = BoardEditor.MoveGroup(config, group.Id, foreignColumn.Id, 0);

        Assert.True(result.Ok);
        Assert.Empty(sourceColumn.Groups);
        Assert.Equal(new[] { group, existing }, foreignColumn.Groups);
        Assert.Same(item, Assert.Single(group.Items));
        Assert.True(group.Collapsed);
    }

    [Fact]
    public void SetGroupColor_sets_the_color()
    {
        var config = ConfigWithTabs("A");
        var group = BoardEditor.AddGroup(config.Tabs[0].Columns[0], "G", Blue);

        var result = BoardEditor.SetGroupColor(config, group.Id, "#ef4444");

        Assert.True(result.Ok);
        Assert.Equal("#ef4444", group.Color);
    }

    [Fact]
    public void SetCollapsed_sets_flag()
    {
        var config = ConfigWithTabs("A");
        var group = BoardEditor.AddGroup(config.Tabs[0].Columns[0], "G", Blue);

        Assert.True(BoardEditor.SetCollapsed(config, group.Id, true).Ok);
        Assert.True(group.Collapsed);

        Assert.True(BoardEditor.SetCollapsed(config, group.Id, false).Ok);
        Assert.False(group.Collapsed);
    }

    // ----- Items -----

    [Fact]
    public void AddItem_appends_and_returns_the_item()
    {
        var config = ConfigWithTabs("A");
        var group = BoardEditor.AddGroup(config.Tabs[0].Columns[0], "G", Blue);
        var item = FolderItem(@"D:\Clients");

        var returned = BoardEditor.AddItem(group, item);

        Assert.Same(item, returned);
        Assert.Same(item, Assert.Single(group.Items));
    }

    [Fact]
    public void RemoveItem_removes_it_from_its_group()
    {
        var config = ConfigWithTabs("A");
        var group = BoardEditor.AddGroup(config.Tabs[0].Columns[0], "G", Blue);
        var keep = BoardEditor.AddItem(group, FolderItem(@"D:\Keep"));
        var drop = BoardEditor.AddItem(group, FolderItem(@"D:\Drop"));

        var result = BoardEditor.RemoveItem(config, drop.Id);

        Assert.True(result.Ok);
        Assert.Same(keep, Assert.Single(group.Items));
    }

    [Fact]
    public void MoveItem_between_groups_preserves_item_identity()
    {
        var config = ConfigWithTabs("A");
        var column = config.Tabs[0].Columns[0];
        var source = BoardEditor.AddGroup(column, "Source", Blue);
        var target = BoardEditor.AddGroup(column, "Target", Blue);
        var moved = BoardEditor.AddItem(source, FolderItem(@"D:\Moved"));
        var stay = BoardEditor.AddItem(source, FolderItem(@"D:\Stay"));
        var first = BoardEditor.AddItem(target, FolderItem(@"D:\First"));
        var originalId = moved.Id;

        var result = BoardEditor.MoveItem(config, moved.Id, target.Id, 1);

        Assert.True(result.Ok);
        Assert.Equal(new[] { stay }, source.Items);
        Assert.Equal(new[] { first, moved }, target.Items);
        Assert.Same(moved, target.Items[1]);
        Assert.Equal(originalId, target.Items[1].Id);
    }

    [Fact]
    public void MoveItem_clamps_out_of_range_indexes()
    {
        var config = ConfigWithTabs("A");
        var column = config.Tabs[0].Columns[0];
        var source = BoardEditor.AddGroup(column, "Source", Blue);
        var target = BoardEditor.AddGroup(column, "Target", Blue);
        var a = BoardEditor.AddItem(source, FolderItem(@"D:\A"));
        var b = BoardEditor.AddItem(source, FolderItem(@"D:\B"));
        var existing = BoardEditor.AddItem(target, FolderItem(@"D:\Existing"));

        BoardEditor.MoveItem(config, a.Id, target.Id, 99);
        Assert.Equal(new[] { existing, a }, target.Items);

        BoardEditor.MoveItem(config, b.Id, target.Id, -1);
        Assert.Equal(new[] { b, existing, a }, target.Items);
    }

    [Fact]
    public void MoveItem_may_cross_tabs()
    {
        var config = ConfigWithTabs("A", "B");
        var source = BoardEditor.AddGroup(config.Tabs[0].Columns[0], "Source", Blue);
        var target = BoardEditor.AddGroup(config.Tabs[1].Columns[0], "Target", Blue);
        var item = BoardEditor.AddItem(source, FolderItem(@"D:\X"));

        var result = BoardEditor.MoveItem(config, item.Id, target.Id, 0);

        Assert.True(result.Ok);
        Assert.Empty(source.Items);
        Assert.Same(item, Assert.Single(target.Items));
    }

    // ----- Rename -----

    [Fact]
    public void Rename_rejects_blank()
    {
        var config = ConfigWithTabs("Original");
        var tab = config.Tabs[0];

        var result = BoardEditor.Rename(config, tab.Id, "   ");

        Assert.Equal(EditError.EmptyName, result.Error);
        Assert.Equal("Original", tab.Name);
    }

    [Fact]
    public void Rename_trims()
    {
        var config = ConfigWithTabs("Original");
        var tab = config.Tabs[0];

        var result = BoardEditor.Rename(config, tab.Id, "  Projets  ");

        Assert.True(result.Ok);
        Assert.Equal("Projets", tab.Name);
    }

    [Fact]
    public void Rename_applies_to_tabs_groups_and_items()
    {
        var config = ConfigWithTabs("Tab");
        var group = BoardEditor.AddGroup(config.Tabs[0].Columns[0], "Group", Blue);
        var item = BoardEditor.AddItem(group, FolderItem(@"D:\Clients"));

        Assert.True(BoardEditor.Rename(config, config.Tabs[0].Id, "Tab 2").Ok);
        Assert.True(BoardEditor.Rename(config, group.Id, "Group 2").Ok);
        Assert.True(BoardEditor.Rename(config, item.Id, "Clients").Ok);

        Assert.Equal("Tab 2", config.Tabs[0].Name);
        Assert.Equal("Group 2", group.Name);
        Assert.Equal("Clients", item.Name);
    }

    // ----- Unknown ids -----

    [Fact]
    public void Operations_report_NotFound_for_unknown_ids()
    {
        var config = ConfigWithTabs("A");
        var tab = config.Tabs[0];
        var group = BoardEditor.AddGroup(tab.Columns[0], "G", Blue);
        var item = BoardEditor.AddItem(group, FolderItem(@"D:\X"));
        var unknown = Guid.NewGuid();

        Assert.Equal(EditError.NotFound, BoardEditor.RemoveTab(config, unknown).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.MoveTab(config, unknown, 0).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.RemoveColumn(tab, unknown).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.SetColumnWidth(tab, unknown, 200).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.MoveColumn(config, unknown, tab.Id, 0).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.RemoveGroup(config, unknown).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.MoveGroup(config, unknown, tab.Columns[1].Id, 0).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.MoveGroup(config, group.Id, unknown, 0).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.SetGroupColor(config, unknown, Blue).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.SetCollapsed(config, unknown, true).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.RemoveItem(config, unknown).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.MoveItem(config, unknown, group.Id, 0).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.MoveItem(config, item.Id, unknown, 0).Error);
        Assert.Equal(EditError.NotFound, BoardEditor.Rename(config, unknown, "X").Error);

        Assert.Same(item, Assert.Single(group.Items));
    }

    // ----- ContainsTarget -----

    [Fact]
    public void ContainsTarget_matches_case_and_trailing_slash_insensitively()
    {
        var group = new Group();
        group.Items.Add(FolderItem(@"D:\Clients\"));
        group.Items.Add(new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.Desktop });

        Assert.True(BoardEditor.ContainsTarget(group, FolderItem(@"d:\clients")));
        Assert.True(BoardEditor.ContainsTarget(group, new Item { Special = SpecialFolderKey.Desktop }));
    }

    [Fact]
    public void ContainsTarget_matches_same_site()
    {
        var group = new Group();
        group.Items.Add(new Item { Kind = ItemKind.Url, Url = "https://example.com/" });

        Assert.True(BoardEditor.ContainsTarget(group, new Item { Kind = ItemKind.Url, Url = "https://Example.com" }));
        Assert.False(BoardEditor.ContainsTarget(group, new Item { Kind = ItemKind.Url, Url = "https://example.org/" }));
        Assert.False(BoardEditor.ContainsTarget(group, FolderItem(@"D:\Clients")));
    }

    [Fact]
    public void ContainsTarget_does_not_match_different_targets()
    {
        var group = new Group();
        group.Items.Add(FolderItem(@"D:\Clients"));
        group.Items.Add(new Item { Kind = ItemKind.Folder, Special = SpecialFolderKey.Desktop });

        Assert.False(BoardEditor.ContainsTarget(group, FolderItem(@"D:\Clients\Acme")));
        Assert.False(BoardEditor.ContainsTarget(group, FolderItem(@"E:\Clients")));
        Assert.False(BoardEditor.ContainsTarget(group, new Item { Special = SpecialFolderKey.Documents }));
        Assert.False(BoardEditor.ContainsTarget(new Group(), FolderItem(@"D:\Clients")));
    }
}
