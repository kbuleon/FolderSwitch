// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>A named board made of <see cref="Column"/>s.</summary>
public sealed class Tab
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The emoji shown before the name. <see langword="null"/> with <see cref="IconChosen"/> false is the
    /// automatic, empty icon; <see langword="null"/> with <see cref="IconChosen"/> true is "no icon" chosen.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>True once the user picked the icon (or "no icon"): renaming then never changes it.</summary>
    public bool IconChosen { get; set; }

    public List<Column> Columns { get; set; } = new();
}
