// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>What the emoji font of the machine can draw.</summary>
public interface IEmojiGlyphs
{
    /// <summary>
    /// Whether the font has a glyph for <paramref name="emoji"/> (a catalog emoji, with or without its U+FE0F).
    /// Never throws: <c>false</c> when it cannot tell.
    /// </summary>
    bool CanDraw(string emoji);
}
