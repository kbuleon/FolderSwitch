// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using System.Text.Json;
using FolderSwitch.Core.Icons;

namespace FolderSwitch.Core.Tests.Icons;

/// <summary>Checks on the real emoji data shipped by the application (catalog and keyword files).</summary>
public class EmojiDataFilesTests
{
    private static readonly string[] Categories =
        ["smileysEmotion", "peopleBody", "animalsNature", "foodDrink", "travelPlaces", "activities", "objects", "symbols"];

    private static readonly string[] ExpectedCodes = ["en", "de", "es", "fr", "it", "nl", "pl", "pt-BR", "pt-PT"];

    private static readonly string[] CommonExamples =
        ["💵", "💰", "🏦", "🏠", "💼", "🚀", "🔧", "👥", "🐱", "📁", "📷", "🎬"];

    private const int VariationSelector16 = 0xFE0F;

    private static readonly Lazy<string> EmojiDirectory = new(LocateEmojiDirectory);

    private static readonly Lazy<IReadOnlyList<(string Emoji, string Category)>> Catalog = new(LoadCatalog);

    private static string LocateEmojiDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FolderSwitch.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("FolderSwitch.slnx was not found above " + AppContext.BaseDirectory);
        }

        return Path.Combine(directory.FullName, "src", "FolderSwitch.App", "Emoji");
    }

    private static JsonDocument Read(string relativePath) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(EmojiDirectory.Value, relativePath), Encoding.UTF8));

    private static IReadOnlyList<(string Emoji, string Category)> LoadCatalog()
    {
        using var document = Read("catalog.json");
        return document.RootElement.GetProperty("emoji").EnumerateArray()
            .Select(entry => (entry.GetProperty("e").GetString()!, entry.GetProperty("c").GetString()!))
            .ToList();
    }

    [Fact]
    public void Catalog_has_only_single_code_point_emoji()
    {
        var emojis = Catalog.Value.Select(entry => entry.Emoji).ToList();
        Assert.NotEmpty(emojis);

        foreach (var emoji in emojis)
        {
            var codePoints = emoji.EnumerateRunes().Select(rune => rune.Value).ToList();
            Assert.True(
                codePoints.Count is 1 or 2 && (codePoints.Count == 1 || codePoints[1] == VariationSelector16) && codePoints[0] != VariationSelector16,
                $"{Describe(emoji)} is not one code point, optionally followed by U+FE0F.");
            Assert.DoesNotContain(codePoints, codePoint => codePoint is 0x200D or 0x20E3);
            Assert.DoesNotContain(codePoints, codePoint => codePoint is >= 0x1F3FB and <= 0x1F3FF);
            Assert.DoesNotContain(codePoints, codePoint => codePoint is >= 0x1F1E6 and <= 0x1F1FF);
        }

        Assert.Equal(emojis.Count, emojis.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            emojis.Count,
            emojis.Select(emoji => emoji.Replace("\uFE0F", string.Empty, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>The configuration reader drops an icon that is not plausible: the catalog never offers one.</summary>
    [Fact]
    public void Every_catalog_emoji_is_a_plausible_icon()
    {
        var implausible = Catalog.Value.Select(entry => entry.Emoji).Where(emoji => !IconText.IsPlausible(emoji)).Select(Describe).ToList();

        Assert.Empty(implausible);
    }

    [Fact]
    public void Catalog_categories_are_known_and_in_order()
    {
        var blocks = new List<string>();
        foreach (var (emoji, category) in Catalog.Value)
        {
            Assert.True(Array.IndexOf(Categories, category) >= 0, $"{Describe(emoji)} is in the unknown category '{category}'.");
            if (blocks.Count == 0 || blocks[^1] != category)
            {
                blocks.Add(category);
            }
        }

        // Each category in one block (no category comes back after another), blocks in the order of the constraint.
        Assert.Equal(blocks.Distinct(StringComparer.Ordinal).Count(), blocks.Count);
        Assert.Equal(Categories.Where(blocks.Contains), blocks);
    }

    [Fact]
    public void Catalog_holds_common_examples()
    {
        var emojis = Catalog.Value.Select(entry => entry.Emoji.Replace("\uFE0F", string.Empty, StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        foreach (var example in CommonExamples)
        {
            Assert.Contains(example, emojis);
        }
    }

    [Fact]
    public void Every_language_has_its_keyword_file()
    {
        var catalog = Catalog.Value.Select(entry => entry.Emoji).ToHashSet(StringComparer.Ordinal);

        foreach (var code in ExpectedCodes)
        {
            var path = Path.Combine("keywords", code + ".json");
            Assert.True(File.Exists(Path.Combine(EmojiDirectory.Value, path)), $"{path} is missing.");

            using var document = Read(path);
            var entries = document.RootElement.GetProperty("emoji").EnumerateObject().ToList();
            Assert.NotEmpty(entries);
            Assert.All(entries, entry => Assert.True(catalog.Contains(entry.Name), $"{path} cites {Describe(entry.Name)}, which is not in the catalog."));

            // Every catalog emoji has a name and at least one keyword in every language.
            var named = entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Value.GetProperty("name").GetString())).Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
            Assert.True(catalog.IsSubsetOf(named), $"{path}: no name for {string.Join(' ', catalog.Except(named).Select(Describe))}.");

            var withKeywords = entries
                .Where(entry => entry.Value.GetProperty("keywords").EnumerateArray().Any(keyword => !string.IsNullOrWhiteSpace(keyword.GetString())))
                .Select(entry => entry.Name)
                .ToHashSet(StringComparer.Ordinal);
            Assert.True(catalog.IsSubsetOf(withKeywords), $"{path}: no keyword for {string.Join(' ', catalog.Except(withKeywords).Select(Describe))}.");
        }
    }

    private static string Describe(string emoji) =>
        emoji + " (" + string.Join(' ', emoji.EnumerateRunes().Select(rune => "U+" + rune.Value.ToString("X4", System.Globalization.CultureInfo.InvariantCulture))) + ")";
}
