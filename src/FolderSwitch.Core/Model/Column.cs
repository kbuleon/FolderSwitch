// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>A vertical stack of <see cref="Group"/>s inside a <see cref="Tab"/>.</summary>
public sealed class Column
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Width in pixels; <c>null</c> means automatic.</summary>
    public int? Width { get; set; }

    public List<Group> Groups { get; set; } = new();
}
