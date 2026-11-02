// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Editing;

/// <summary>
/// Pure editing operations on the configuration model (no I/O, no UI).
/// Operations that take an <see cref="AppConfig"/> look the target up by id anywhere in the config.
/// Insertion indexes are positions in the resulting list and are clamped: negative becomes 0,
/// too large becomes the end.
/// </summary>
public static class BoardEditor
{
    private const int MinColumnWidth = 150;
    private const int MaxColumnWidth = 520;
    private const int ColumnWidthStep = 10;

    private static readonly EditResult Success = new(EditError.None);

    // ----- Tabs -----

    public static Tab AddTab(AppConfig c, string name)
    {
        var tab = new Tab { Name = name };
        tab.Columns.Add(new Column());
        tab.Columns.Add(new Column());
        c.Tabs.Add(tab);
        return tab;
    }

    public static EditResult RemoveTab(AppConfig c, Guid tabId)
    {
        var index = c.Tabs.FindIndex(t => t.Id == tabId);
        if (index < 0)
        {
            return Fail(EditError.NotFound);
        }

        if (c.Tabs.Count == 1)
        {
            return Fail(EditError.LastTabCannotBeRemoved);
        }

        c.Tabs.RemoveAt(index);
        return Success;
    }

    public static EditResult MoveTab(AppConfig c, Guid tabId, int newIndex)
    {
        var index = c.Tabs.FindIndex(t => t.Id == tabId);
        if (index < 0)
        {
            return Fail(EditError.NotFound);
        }

        MoveWithin(c.Tabs, index, newIndex);
        return Success;
    }

    // ----- Columns -----

    public static Column AddColumn(Tab t)
    {
        var column = new Column();
        t.Columns.Add(column);
        return column;
    }

    /// <summary>Removes an empty column; a tab keeps at least one column.</summary>
    public static EditResult RemoveColumn(Tab t, Guid columnId)
    {
        var index = t.Columns.FindIndex(col => col.Id == columnId);
        if (index < 0)
        {
            return Fail(EditError.NotFound);
        }

        if (t.Columns.Count == 1)
        {
            return Fail(EditError.LastColumnCannotBeRemoved);
        }

        if (t.Columns[index].Groups.Count > 0)
        {
            return Fail(EditError.ColumnNotEmpty);
        }

        t.Columns.RemoveAt(index);
        return Success;
    }

    /// <summary>
    /// Moves a column, with its groups and its width, within its tab or to another tab. The last column of a tab does
    /// not leave it: a tab keeps at least one column.
    /// </summary>
    public static EditResult MoveColumn(AppConfig c, Guid columnId, Guid targetTabId, int index)
    {
        if (FindColumn(c, columnId) is not { } source || c.Tabs.Find(t => t.Id == targetTabId) is not { } target)
        {
            return Fail(EditError.NotFound);
        }

        if (!ReferenceEquals(source.Tab, target) && source.Tab.Columns.Count == 1)
        {
            return Fail(EditError.LastColumnCannotBeRemoved);
        }

        source.Tab.Columns.Remove(source.Column);
        InsertClamped(target.Columns, index, source.Column);
        return Success;
    }

    /// <summary>Sets the width of a column; <c>null</c> means automatic, otherwise the width is clamped.</summary>
    public static EditResult SetColumnWidth(Tab t, Guid columnId, int? width)
    {
        var column = t.Columns.Find(col => col.Id == columnId);
        if (column is null)
        {
            return Fail(EditError.NotFound);
        }

        column.Width = width is { } requested ? ClampColumnWidth(requested) : null;
        return Success;
    }

    /// <summary>Rounds to the nearest multiple of 10 (midpoints go up), then clamps to 150..520.</summary>
    public static int ClampColumnWidth(int requested)
    {
        // Clamping first is equivalent (both bounds are multiples of the step) and cannot overflow.
        var clamped = Math.Clamp(requested, MinColumnWidth, MaxColumnWidth);
        return (clamped + ColumnWidthStep / 2) / ColumnWidthStep * ColumnWidthStep;
    }

    // ----- Groups -----

    public static Group AddGroup(Column col, string name, string colorHex)
    {
        var group = new Group { Name = name, Color = colorHex };
        col.Groups.Add(group);
        return group;
    }

    public static EditResult RemoveGroup(AppConfig c, Guid groupId)
    {
        if (FindGroup(c, groupId) is not { } found)
        {
            return Fail(EditError.NotFound);
        }

        found.Column.Groups.Remove(found.Group);
        return Success;
    }

    /// <summary>Moves a group to any column, in any tab, keeping the same <see cref="Group"/> instance.</summary>
    public static EditResult MoveGroup(AppConfig c, Guid groupId, Guid targetColumnId, int index)
    {
        if (FindGroup(c, groupId) is not { } source || FindColumn(c, targetColumnId) is not { } target)
        {
            return Fail(EditError.NotFound);
        }

        source.Column.Groups.Remove(source.Group);
        InsertClamped(target.Column.Groups, index, source.Group);
        return Success;
    }

    public static EditResult SetGroupColor(AppConfig c, Guid groupId, string colorHex)
    {
        if (FindGroup(c, groupId) is not { } found)
        {
            return Fail(EditError.NotFound);
        }

        found.Group.Color = colorHex;
        return Success;
    }

    public static EditResult SetCollapsed(AppConfig c, Guid groupId, bool collapsed)
    {
        if (FindGroup(c, groupId) is not { } found)
        {
            return Fail(EditError.NotFound);
        }

        found.Group.Collapsed = collapsed;
        return Success;
    }

    // ----- Items -----

    public static Item AddItem(Group g, Item item)
    {
        g.Items.Add(item);
        return item;
    }

    public static EditResult RemoveItem(AppConfig c, Guid itemId)
    {
        if (FindItem(c, itemId) is not { } found)
        {
            return Fail(EditError.NotFound);
        }

        found.Group.Items.Remove(found.Item);
        return Success;
    }

    /// <summary>Moves an item to any group, in any tab, keeping the same <see cref="Item"/> instance.</summary>
    public static EditResult MoveItem(AppConfig c, Guid itemId, Guid targetGroupId, int index)
    {
        if (FindItem(c, itemId) is not { } source || FindGroup(c, targetGroupId) is not { } target)
        {
            return Fail(EditError.NotFound);
        }

        source.Group.Items.Remove(source.Item);
        InsertClamped(target.Group.Items, index, source.Item);
        return Success;
    }

    // ----- Rename -----

    /// <summary>Renames a tab, a group or an item. The name is trimmed; a blank name is refused.</summary>
    public static EditResult Rename(AppConfig c, Guid id, string newName)
    {
        var name = newName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Fail(EditError.EmptyName);
        }

        if (c.Tabs.Find(t => t.Id == id) is { } tab)
        {
            tab.Name = name;
        }
        else if (FindGroup(c, id) is { } group)
        {
            group.Group.Name = name;
        }
        else if (FindItem(c, id) is { } item)
        {
            item.Item.Name = name;
        }
        else
        {
            return Fail(EditError.NotFound);
        }

        return Success;
    }

    // ----- Duplicates -----

    /// <summary>
    /// True when the group already holds the same target: same <see cref="Item.Special"/>, or same
    /// <see cref="Item.Path"/> ignoring case and a trailing backslash, or same web site (<see cref="WebAddress.SameSite"/>).
    /// </summary>
    public static bool ContainsTarget(Group g, Item candidate)
    {
        return g.Items.Any(existing => SameTarget(existing, candidate));
    }

    private static bool SameTarget(Item a, Item b)
    {
        if (a.Kind == ItemKind.Url || b.Kind == ItemKind.Url)
        {
            return a.Kind == b.Kind && a.Url is not null && b.Url is not null && WebAddress.SameSite(a.Url, b.Url);
        }

        if (a.Special is { } special)
        {
            return b.Special == special;
        }

        if (b.Special is not null || a.Path is null || b.Path is null)
        {
            return false;
        }

        return string.Equals(NormalizePath(a.Path), NormalizePath(b.Path), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) => path.TrimEnd('\\');

    // ----- Helpers -----

    private static EditResult Fail(EditError error) => new(error);

    private static void MoveWithin<T>(List<T> list, int oldIndex, int newIndex)
    {
        var element = list[oldIndex];
        list.RemoveAt(oldIndex);
        InsertClamped(list, newIndex, element);
    }

    private static void InsertClamped<T>(List<T> list, int index, T element)
    {
        list.Insert(Math.Clamp(index, 0, list.Count), element);
    }

    private static (Tab Tab, Column Column)? FindColumn(AppConfig c, Guid columnId)
    {
        foreach (var tab in c.Tabs)
        {
            foreach (var column in tab.Columns)
            {
                if (column.Id == columnId)
                {
                    return (tab, column);
                }
            }
        }

        return null;
    }

    private static (Tab Tab, Column Column, Group Group)? FindGroup(AppConfig c, Guid groupId)
    {
        foreach (var tab in c.Tabs)
        {
            foreach (var column in tab.Columns)
            {
                foreach (var group in column.Groups)
                {
                    if (group.Id == groupId)
                    {
                        return (tab, column, group);
                    }
                }
            }
        }

        return null;
    }

    private static (Group Group, Item Item)? FindItem(AppConfig c, Guid itemId)
    {
        foreach (var tab in c.Tabs)
        {
            foreach (var column in tab.Columns)
            {
                foreach (var group in column.Groups)
                {
                    foreach (var item in group.Items)
                    {
                        if (item.Id == itemId)
                        {
                            return (group, item);
                        }
                    }
                }
            }
        }

        return null;
    }
}
