// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Icons;

/// <summary>
/// An emoji of the catalog: <paramref name="Emoji"/> as Unicode writes it (one code point, followed by U+FE0F when
/// Unicode asks for it), its <paramref name="Category"/>, and its <paramref name="Order"/> in the catalog.
/// </summary>
public sealed record EmojiEntry(string Emoji, EmojiCategory Category, int Order);
