// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>What an <see cref="Item"/> points to.</summary>
public enum ItemKind
{
    Folder,
    File,

    /// <summary>A site: the item holds an <c>http</c>, <c>https</c>, <c>ftp</c> or <c>file</c> address in <see cref="Item.Url"/>.</summary>
    Url,

    /// <summary>
    /// A plain horizontal line between the items of a group, as in a bookmark manager: no name, no icon, no target. It
    /// is moved and removed like an item, and never opened.
    /// </summary>
    Separator,
}
