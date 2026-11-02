// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>
/// A folder, file or web site shortcut inside a <see cref="Group"/>.
/// Exactly one of <see cref="Path"/>, <see cref="Special"/> and <see cref="Url"/> is set.
/// </summary>
public sealed class Item
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ItemKind Kind { get; set; }

    /// <summary>Display name chosen by the user; <c>null</c> means the default name.</summary>
    public string? Name { get; set; }

    public string? Path { get; set; }

    public SpecialFolderKey? Special { get; set; }

    /// <summary>The <c>http</c> or <c>https</c> address of an item of kind <see cref="ItemKind.Url"/>.</summary>
    public string? Url { get; set; }
}
