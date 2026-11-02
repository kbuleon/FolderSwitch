// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>What an <see cref="Item"/> points to.</summary>
public enum ItemKind
{
    Folder,
    File,

    /// <summary>A web site: the item holds an <c>http</c> or <c>https</c> address in <see cref="Item.Url"/>.</summary>
    Url,
}
