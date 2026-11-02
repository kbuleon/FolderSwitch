// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.Text.RegularExpressions;

namespace FolderSwitch.EmojiData;

/// <summary>One line of <c>emoji-test.txt</c>.</summary>
/// <param name="Emoji">The emoji as written in the file (its code points, U+FE0F included).</param>
/// <param name="Status">component, fully-qualified, minimally-qualified or unqualified.</param>
/// <param name="Group">The Unicode group (the catalog's category), as the file names it.</param>
/// <param name="Name">The Unicode name, for the contact sheets and the report.</param>
internal sealed record EmojiTestEntry(string Emoji, string Status, string Group, string Name);

/// <summary>Reads <c>emoji-test.txt</c> and decides which of its emojis may enter the catalog.</summary>
internal static partial class EmojiTestFile
{
    public const int VariationSelector16 = 0xFE0F;

    /// <summary>The catalog's categories in catalog order, with the Unicode group each one comes from.</summary>
    public static readonly IReadOnlyList<(string Category, string Group)> Categories =
    [
        ("smileysEmotion", "Smileys & Emotion"),
        ("peopleBody", "People & Body"),
        ("animalsNature", "Animals & Nature"),
        ("foodDrink", "Food & Drink"),
        ("travelPlaces", "Travel & Places"),
        ("activities", "Activities"),
        ("objects", "Objects"),
        ("symbols", "Symbols"),
    ];

    /// <summary>The version the file declares in its header (<c># Version: 18.0</c>), or null.</summary>
    public static string? ReadVersion(string text)
    {
        var match = VersionLine().Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static IReadOnlyList<EmojiTestEntry> Parse(string text)
    {
        var entries = new List<EmojiTestEntry>();
        var group = string.Empty;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# group:", StringComparison.Ordinal))
            {
                group = trimmed["# group:".Length..].Trim();
                continue;
            }

            var match = DataLine().Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            var emoji = string.Concat(match.Groups[1].Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(hex => char.ConvertFromUtf32(int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture))));
            entries.Add(new EmojiTestEntry(emoji, match.Groups[2].Value, group, match.Groups[3].Value));
        }

        return entries;
    }

    /// <summary>
    /// The catalog's category of a line, or null when the line cannot be in the catalog: not fully-qualified, not one
    /// code point (optionally followed by U+FE0F), a skin tone, a regional indicator, or in no catalog category.
    /// </summary>
    public static string? CategoryOf(EmojiTestEntry entry)
    {
        if (entry.Status != "fully-qualified")
        {
            return null;
        }

        var codePoints = entry.Emoji.EnumerateRunes().Select(rune => rune.Value).Where(value => value != VariationSelector16).ToList();
        if (codePoints.Count != 1 || codePoints[0] is (>= 0x1F3FB and <= 0x1F3FF) or (>= 0x1F1E6 and <= 0x1F1FF))
        {
            return null;
        }

        foreach (var (category, group) in Categories)
        {
            if (group == entry.Group)
            {
                return category;
            }
        }

        return null;
    }

    /// <summary>The emoji without its variation selector: the key under which CLDR, the hand list and the font know it.</summary>
    public static string BaseOf(string emoji) => emoji.Replace("\uFE0F", string.Empty, StringComparison.Ordinal);

    // 1F600                                                  ; fully-qualified     # 😀 E1.0 grinning face
    [GeneratedRegex(@"^([0-9A-Fa-f]+(?: [0-9A-Fa-f]+)*)\s*;\s*(\S+)\s*#\s*\S+\s+E\d+\.\d+\s+(.+)$")]
    private static partial Regex DataLine();

    [GeneratedRegex(@"^# Version: (\d+\.\d+)", RegexOptions.Multiline)]
    private static partial Regex VersionLine();
}
