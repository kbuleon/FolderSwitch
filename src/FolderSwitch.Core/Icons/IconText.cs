// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;

namespace FolderSwitch.Core.Icons;

/// <summary>
/// Structural check of the text of an icon. It knows nothing of the emoji catalog: it only tells an icon
/// from stray text, so that a damaged or hand-edited configuration never shows a word as an icon.
/// </summary>
public static class IconText
{
    private const int FirstPlausibleCodePoint = 0xA9;

    private const int VariationSelector16 = 0xFE0F;

    /// <summary>
    /// True for one or two Unicode scalar values, the second one being only U+FE0F, the first one at least
    /// U+00A9 and neither a letter, a digit nor a white space. Skin tones, ZWJ sequences, flags and keycaps
    /// are not plausible icons: the catalog holds none of them.
    /// </summary>
    public static bool IsPlausible(string? icon)
    {
        if (string.IsNullOrEmpty(icon))
        {
            return false;
        }

        var count = 0;
        foreach (var rune in icon.EnumerateRunes())
        {
            count++;
            if (count == 1)
            {
                if (rune.Value < FirstPlausibleCodePoint
                    || rune == Rune.ReplacementChar
                    || Rune.IsLetterOrDigit(rune)
                    || Rune.IsWhiteSpace(rune))
                {
                    return false;
                }
            }
            else if (count > 2 || rune.Value != VariationSelector16)
            {
                return false;
            }
        }

        return true;
    }
}
