// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Core.Tests.Icons;

public class IconFinderTests
{
    // In catalog order: the house comes before the cat, and the people before the folder, so that a tie broken by the
    // catalog would give the other answer than the rule under test.
    private static readonly EmojiEntry[] Entries =
    [
        new("😺", EmojiCategory.SmileysEmotion, 0),
        new("🧑", EmojiCategory.PeopleBody, 1),
        new("👔", EmojiCategory.PeopleBody, 2),
        new("🏠", EmojiCategory.TravelPlaces, 3),
        new("🐱", EmojiCategory.AnimalsNature, 4),
        new("🏦", EmojiCategory.TravelPlaces, 5),
        new("🚀", EmojiCategory.TravelPlaces, 6),
        new("💵", EmojiCategory.Objects, 7),
        new("💰", EmojiCategory.Objects, 8),
        new("📁", EmojiCategory.Objects, 9),
    ];

    private static readonly EmojiKeywords French = Keywords(
        "fr",
        ("😺", "chaton souriant", ["chaton", "sourire"]),
        ("🧑", "adulte", ["adulte", "dossier", "personne"]),
        ("👔", "cravate", ["cravate", "vêtement"]),
        ("🏠", "maison", ["domicile", "maison", "zèbre"]),
        ("🐱", "tête de chat", ["animal", "chat"]),
        ("🏦", "banque", ["banque", "finance"]),
        ("💵", "billet de dollar", ["argent liquide", "billet", "dollar"]),
        ("💰", "sac d’argent", ["argent", "sac"]),
        ("📁", "dossier de fichiers", ["dossier", "fichier"]));

    private static readonly EmojiKeywords English = Keywords(
        "en",
        ("😺", "grinning cat", ["cat", "grinning"]),
        ("🧑", "person", ["adult", "person"]),
        ("👔", "necktie", ["clothing", "tie"]),
        ("🏠", "house", ["home", "house"]),
        ("🐱", "cat face", ["cat", "pet"]),
        ("🏦", "bank", ["bank", "finance"]),
        ("🚀", "rocket", ["rocket", "space"]),
        ("💵", "dollar banknote", ["banknote", "dollar"]),
        ("💰", "money bag", ["bag", "money"]),
        ("📁", "file folder", ["file", "folder"]));

    private static readonly SynonymConcept[] Concepts =
    [
        Concept("💵", ("fr", ["finance", "argent"]), ("en", ["cash"])),
        Concept("🚀", ("fr", ["atlas"])),
        Concept("📁", ("en", ["atlas"])),
        Concept("💰", ("de", ["atlas", "Zèbre"])),
    ];

    private static readonly Dictionary<string, IReadOnlyList<string>> StopWords = new()
    {
        ["fr"] = ["de", "et", "la", "le"],
        ["en"] = ["and", "of", "the"],
    };

    private static IconFinder Finder(params string[] undrawable)
    {
        var library = new EmojiLibrary(
            new EmojiCatalog(Entries),
            new SynonymTable(Concepts, StopWords),
            code => code switch
            {
                "fr" => French,
                "en" => English,
                _ => EmojiKeywords.Empty(code),
            });
        return new IconFinder(library, new FakeGlyphs(undrawable));
    }

    [Fact]
    public void Synonym_wins_over_keyword()
    {
        Assert.Equal("💵", Finder().Suggest("Finances", "fr"));
    }

    [Fact]
    public void Current_language_synonym_wins_then_english_then_others()
    {
        var finder = Finder();

        Assert.Equal("🚀", finder.Suggest("Atlas", "fr"));
        Assert.Equal("📁", finder.Suggest("Atlas", "en"));
        Assert.Equal("📁", finder.Suggest("Atlas", "es"));

        // A synonym of another language still beats a keyword of the current one.
        Assert.Equal("💰", finder.Suggest("Zèbre", "fr"));
    }

    [Fact]
    public void French_name_is_found_with_english_interface()
    {
        Assert.Equal("💵", Finder().Suggest("Finance", "en"));
    }

    [Fact]
    public void Objects_beat_people()
    {
        Assert.Equal("📁", Finder().Suggest("Dossier", "fr"));
    }

    [Fact]
    public void First_word_wins_a_tie()
    {
        Assert.Equal("🐱", Finder().Suggest("chat maison", "fr"));
        Assert.Equal("🏠", Finder().Suggest("maison chat", "fr"));
    }

    [Fact]
    public void English_keyword_is_the_fallback()
    {
        Assert.Equal("🚀", Finder().Suggest("Rockets", "fr"));
    }

    [Fact]
    public void Nothing_matches_gives_null()
    {
        var finder = Finder();

        Assert.Null(finder.Suggest("Dropbox", "fr"));
        Assert.Null(finder.Suggest("2026", "fr"));
        Assert.Null(finder.Suggest("", "fr"));
        Assert.Null(finder.Suggest("de la", "fr"));
    }

    [Fact]
    public void Undrawable_emoji_is_never_suggested_nor_listed()
    {
        var finder = Finder("💵");

        Assert.Equal("🏦", finder.Suggest("Finance", "fr"));
        Assert.DoesNotContain(finder.Drawable, entry => entry.Emoji == "💵");
        Assert.Equal(Entries.Length - 1, finder.Drawable.Count);
        Assert.DoesNotContain(finder.Filter("fin", "fr"), entry => entry.Emoji == "💵");
        Assert.False(finder.CanShow("💵"));
    }

    [Fact]
    public void CanShow_needs_a_drawable_catalog_emoji()
    {
        var finder = Finder();

        Assert.True(finder.CanShow("📁"));
        Assert.False(finder.CanShow("🎉"));
        Assert.False(finder.CanShow(null));
        Assert.False(finder.CanShow(""));
    }

    [Fact]
    public void Filter_matches_word_starts()
    {
        var finder = Finder();

        Assert.Contains(finder.Filter("fin", "fr"), entry => entry.Emoji == "💵");
        Assert.Equal(["🐱", "😺"], finder.Filter("chat", "fr").Select(entry => entry.Emoji));
        Assert.Empty(finder.Filter("chat zzz", "fr"));
        Assert.Equal(finder.Drawable, finder.Filter("  ", "fr"));
    }

    [Fact]
    public void Filter_ranks_whole_words_then_characters_last()
    {
        var finder = Finder();

        // « dossier »: a whole word of the person 🧑 (a character, first in the catalog) and of the folder 📁: the folder
        // first. « chat »: a whole word of 🐱, only a word start of 😺 (« chaton »), first in the catalog.
        Assert.Equal(["📁", "🧑"], finder.Filter("dossier", "fr").Select(entry => entry.Emoji));
        Assert.Equal(["🐱", "😺"], finder.Filter("chat", "fr").Select(entry => entry.Emoji));

        // « ca », in English: word starts only (cat, cash): 🐱 and 💵 before the smiley 😺, first in the catalog.
        Assert.Equal(["🐱", "💵", "😺"], finder.Filter("ca", "en").Select(entry => entry.Emoji));
    }

    [Fact]
    public void Filter_needs_every_typed_word()
    {
        var finder = Finder();

        Assert.Equal(["💵"], finder.Filter("billet dol", "fr").Select(entry => entry.Emoji));
        Assert.Equal(["🏦"], finder.Filter("fin banq", "fr").Select(entry => entry.Emoji));
    }

    [Fact]
    public void NameOf_falls_back_to_english()
    {
        var finder = Finder();

        Assert.Equal("maison", finder.NameOf("🏠", "fr"));
        Assert.Equal("rocket", finder.NameOf("🚀", "fr"));
        Assert.Equal("rocket", finder.NameOf("🚀", "pl"));
        Assert.Null(finder.NameOf("🎉", "fr"));
    }

    private static EmojiKeywords Keywords(string code, params (string Emoji, string Name, string[] Keywords)[] entries) =>
        new(code, entries.ToDictionary(entry => entry.Emoji, entry => (entry.Name, (IReadOnlyList<string>)entry.Keywords)));

    private static SynonymConcept Concept(string emoji, params (string Code, string[] Words)[] words) =>
        new(emoji, words.ToDictionary(pair => pair.Code, pair => (IReadOnlyList<string>)pair.Words));

    private sealed class FakeGlyphs(string[] undrawable) : IEmojiGlyphs
    {
        public bool CanDraw(string emoji) => !undrawable.Contains(emoji);
    }
}
