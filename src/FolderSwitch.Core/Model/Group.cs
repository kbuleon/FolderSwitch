// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>A named, colored block of <see cref="Item"/>s inside a <see cref="Column"/>.</summary>
public sealed class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Header color as a hex string, for example <c>#3b82f6</c>.</summary>
    public string Color { get; set; } = string.Empty;

    public bool Collapsed { get; set; }

    public List<Item> Items { get; set; } = new();
}
