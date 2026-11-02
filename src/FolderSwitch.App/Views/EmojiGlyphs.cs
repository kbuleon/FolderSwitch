// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Services;

namespace FolderSwitch.App.Views;

/// <summary>
/// What the emoji font of the machine (<see cref="FontName"/>, the <c>Fs.EmojiFont</c> of the views) can draw: an
/// emoji can be drawn when it has the form of an icon (<see cref="IconText.IsPlausible"/>: the font also has glyphs
/// for plain text, letters and digits) and every code point of it, U+FE0F (the emoji presentation selector) left out,
/// has a glyph in the font's character map. The font is opened once, by the constructor; without it,
/// nothing can be drawn.
/// </summary>
/// <remarks>
/// Safe between threads (the font's character map is read under a lock). Never throws: a font that cannot be opened
/// or read answers <c>false</c>, since the icon finder keeps its first answers for the whole session.
/// </remarks>
internal sealed class EmojiGlyphs : IEmojiGlyphs
{
    /// <summary>The emoji font of Windows 10 and 11.</summary>
    public const string FontName = "Segoe UI Emoji";

    private const int PresentationSelector = 0xFE0F;

    private readonly Lock _lock = new();
    private readonly IDictionary<int, ushort>? _characters;

    public EmojiGlyphs()
    {
        _characters = OpenCharacterMap();
    }

    public bool CanDraw(string emoji)
    {
        if (_characters is not { } characters || !IconText.IsPlausible(emoji))
        {
            return false;
        }

        try
        {
            var any = false;
            lock (_lock)
            {
                foreach (var rune in emoji.EnumerateRunes())
                {
                    if (rune.Value == PresentationSelector)
                    {
                        continue;
                    }

                    if (!characters.ContainsKey(rune.Value))
                    {
                        return false;
                    }

                    any = true;
                }
            }

            return any;
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            var codes = string.Join(' ', emoji.EnumerateRunes().Select(rune => "U+" + rune.Value.ToString("X4", CultureInfo.InvariantCulture)));
            DebugLog.Warn($"The emoji font could not be read for {codes}", e);
            return false;
        }
    }

    /// <summary>
    /// The character map of <see cref="FontName"/>; <c>null</c> when the font is missing (WPF would then fall back on
    /// another font, whose glyphs are not the emoji font's) or cannot be read.
    /// </summary>
    private static IDictionary<int, ushort>? OpenCharacterMap()
    {
        try
        {
            var typeface = new Typeface(new FontFamily(FontName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyphs)
                || !glyphs.Win32FamilyNames.Values.Any(name => string.Equals(name, FontName, StringComparison.OrdinalIgnoreCase)))
            {
                DebugLog.Warn($"The emoji font « {FontName} » is missing: no icon can be shown");
                return null;
            }

            return glyphs.CharacterToGlyphMap;
        }
        catch (Exception e) when (!ExceptionPolicy.IsCritical(e))
        {
            DebugLog.Warn($"The emoji font « {FontName} » could not be opened: no icon can be shown", e);
            return null;
        }
    }
}
