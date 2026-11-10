// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text.Json.Serialization;

namespace FolderSwitch.Core.Model;

/// <summary>
/// A folder, file or web site shortcut inside a <see cref="Group"/>, or a separator between them.
/// Exactly one of <see cref="Path"/>, <see cref="Special"/> and <see cref="Url"/> is set, except for a separator
/// (<see cref="ItemKind.Separator"/>), which has none of them and no <see cref="Name"/>.
/// </summary>
public sealed class Item
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public ItemKind Kind { get; set; }

    /// <summary>Display name chosen by the user; <c>null</c> means the default name.</summary>
    public string? Name { get; set; }

    public string? Path { get; set; }

    public SpecialFolderKey? Special { get; set; }

    /// <summary>The <c>http</c>, <c>https</c>, <c>ftp</c> or <c>file</c> address of an item of kind <see cref="ItemKind.Url"/>.</summary>
    public string? Url { get; set; }

    /// <summary>True for a separator (<see cref="ItemKind.Separator"/>): a line, with nothing to open.</summary>
    [JsonIgnore]
    public bool IsSeparator => Kind == ItemKind.Separator;
}
