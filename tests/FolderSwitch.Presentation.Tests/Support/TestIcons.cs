// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>A font that draws every emoji except the ones a test refuses.</summary>
internal sealed class FakeEmojiGlyphs : IEmojiGlyphs
{
    /// <summary>The emojis the font cannot draw.</summary>
    public HashSet<string> Refused { get; } = new(StringComparer.Ordinal);

    public bool CanDraw(string emoji) => !Refused.Contains(emoji);
}

/// <summary>
/// A tiny in-memory emoji library: « Finance » leads to 💵, « Vacances » (or « Vacation ») to 🏖, « Acme » to nothing,
/// and « chat » in French filters 🐱 (a whole keyword) before 😺 (« chaton »).
/// </summary>
internal static class TestIcons
{
    private static readonly EmojiEntry[] Entries =
    [
        new("😺", EmojiCategory.SmileysEmotion, 0),
        new("🐱", EmojiCategory.AnimalsNature, 1),
        new("🏖", EmojiCategory.TravelPlaces, 2),
        new("💵", EmojiCategory.Objects, 3),
    ];

    private static readonly EmojiKeywords French = Keywords(
        "fr",
        ("😺", "chaton souriant", ["chaton", "sourire"]),
        ("🐱", "tête de chat", ["animal", "chat"]),
        ("💵", "billet", ["billet"]));

    private static readonly EmojiKeywords English = Keywords(
        "en",
        ("😺", "grinning cat", ["cat", "grinning"]),
        ("🐱", "cat face", ["cat", "pet"]),
        ("🏖", "beach", ["beach"]),
        ("💵", "dollar banknote", ["banknote", "dollar"]));

    /// <summary>A finder over the test library; <paramref name="extra"/> concepts are added to its synonyms.</summary>
    public static IconFinder Finder(IEmojiGlyphs? glyphs = null, params SynonymConcept[] extra)
    {
        var concepts = new List<SynonymConcept>
        {
            Concept("💵", ("fr", ["finance"]), ("en", ["finance"])),
            Concept("🏖", ("fr", ["vacances"]), ("en", ["vacation"])),
        };
        concepts.AddRange(extra);

        var library = new EmojiLibrary(
            new EmojiCatalog(Entries),
            new SynonymTable(concepts, new Dictionary<string, IReadOnlyList<string>>()),
            code => code switch
            {
                "fr" => French,
                "en" => English,
                _ => EmojiKeywords.Empty(code),
            });
        return new IconFinder(library, glyphs ?? new FakeEmojiGlyphs());
    }

    public static Lazy<IconFinder> Lazy(IEmojiGlyphs? glyphs = null, params SynonymConcept[] extra) =>
        new(() => Finder(glyphs, extra));

    public static SynonymConcept Concept(string emoji, params (string Code, string[] Words)[] words) =>
        new(emoji, words.ToDictionary(pair => pair.Code, pair => (IReadOnlyList<string>)pair.Words));

    private static EmojiKeywords Keywords(string code, params (string Emoji, string Name, string[] Keywords)[] entries) =>
        new(code, entries.ToDictionary(entry => entry.Emoji, entry => (entry.Name, (IReadOnlyList<string>)entry.Keywords)));
}
