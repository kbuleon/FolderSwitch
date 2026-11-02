// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Tests.Persistence;

namespace FolderSwitch.Core.Tests.Icons;

public class EmojiLibraryTests
{
    private const string Catalog = """
        {
          "unicodeVersion": "18.0",
          "emoji": [
            { "e": "😀", "c": "smileysEmotion" },
            { "e": "☺️", "c": "smileysEmotion" },
            { "e": "🦄", "c": "flags" },
            { "e": "📁", "c": "objects" }
          ]
        }
        """;

    private const string FrenchKeywords = """
        {
          "cldrVersion": "49",
          "emoji": {
            "😀": { "name": "visage rieur", "keywords": [ "rire", "sourire" ] },
            "☺": { "name": "visage souriant", "keywords": [ "sourire" ] },
            "📁": { "name": "dossier de fichiers", "keywords": [ "dossier", "fichier" ] }
          }
        }
        """;

    private const string Synonyms = """
        {
          "stopWords": { "fr": [ "de", "Été" ] },
          "concepts": [
            { "emoji": "📁", "words": { "fr": [ "dossier" ], "en": [ "folder" ] } }
          ]
        }
        """;

    private static void Write(TempDirectory directory, string relativePath, string content, bool byteOrderMark = false)
    {
        var path = Path.Combine(directory.Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(byteOrderMark));
    }

    [Fact]
    public void Loads_the_catalog_the_synonyms_and_the_keywords()
    {
        using var directory = new TempDirectory();
        Write(directory, "catalog.json", Catalog, byteOrderMark: true);
        Write(directory, "synonyms.json", Synonyms);
        Write(directory, Path.Combine("keywords", "fr.json"), FrenchKeywords);

        var library = EmojiLibrary.LoadFromDirectory(directory.Root);

        // The entry of an unknown category is left out.
        Assert.Equal(
            [
                new EmojiEntry("😀", EmojiCategory.SmileysEmotion, 0),
                new EmojiEntry("☺️", EmojiCategory.SmileysEmotion, 1),
                new EmojiEntry("📁", EmojiCategory.Objects, 2),
            ],
            library.Catalog.All);

        var concept = Assert.Single(library.Synonyms.Concepts);
        Assert.Equal("📁", concept.Emoji);
        Assert.Equal(["dossier"], concept.Words["fr"]);
        Assert.Equal(["folder"], concept.Words["en"]);
        Assert.True(library.Synonyms.StopWords("fr").SetEquals(["de", "ete"]));
        Assert.Empty(library.Synonyms.StopWords("en"));

        var french = library.Keywords("fr");
        Assert.Equal("fr", french.Code);
        Assert.Equal("visage rieur", french.NameOf("😀"));
        Assert.Equal(["rire", "sourire"], french.KeywordsOf("😀"));

        // The keyword file writes the base form, the catalog the form with U+FE0F: they still meet.
        Assert.Equal("visage souriant", french.NameOf("☺️"));
        Assert.Equal("visage souriant", french.NameOf("☺"));
    }

    [Fact]
    public void Catalog_finds_an_emoji_with_or_without_its_variation_selector()
    {
        var catalog = new EmojiCatalog(
        [
            new EmojiEntry("☺️", EmojiCategory.SmileysEmotion, 0),
            new EmojiEntry("📁", EmojiCategory.Objects, 1),
        ]);

        Assert.Equal("☺️", catalog.Find("☺")?.Emoji);
        Assert.Equal("☺️", catalog.Find("☺️")?.Emoji);
        Assert.Equal("📁", catalog.Find("📁\uFE0F")?.Emoji);
        Assert.True(catalog.Contains("☺"));
        Assert.False(catalog.Contains("🎉"));
        Assert.Null(catalog.Find(""));
    }

    [Fact]
    public void Language_without_a_file_has_empty_keywords()
    {
        using var directory = new TempDirectory();
        Write(directory, "catalog.json", Catalog);

        var library = EmojiLibrary.LoadFromDirectory(directory.Root);

        foreach (var code in new[] { "de", "../catalog", "" })
        {
            var keywords = library.Keywords(code);
            Assert.Equal(code, keywords.Code);
            Assert.Null(keywords.NameOf("😀"));
            Assert.Empty(keywords.KeywordsOf("😀"));
        }
    }

    [Fact]
    public void Unreadable_files_give_empty_data_without_throwing()
    {
        using var directory = new TempDirectory();
        Write(directory, "catalog.json", "{ \"emoji\": [ { \"e\": ");
        Write(directory, "synonyms.json", "[ 1, 2 ]");
        Write(directory, Path.Combine("keywords", "fr.json"), "not json");

        var library = EmojiLibrary.LoadFromDirectory(directory.Root);

        Assert.Empty(library.Catalog.All);
        Assert.Empty(library.Synonyms.Concepts);
        Assert.Empty(library.Synonyms.StopWords("fr"));
        Assert.Empty(library.Keywords("fr").KeywordsOf("📁"));
    }

    [Fact]
    public void Missing_directory_gives_an_empty_library()
    {
        using var directory = new TempDirectory();

        var library = EmojiLibrary.LoadFromDirectory(Path.Combine(directory.Root, "absent"));

        Assert.Empty(library.Catalog.All);
        Assert.Empty(library.Synonyms.Concepts);
        Assert.Null(library.Keywords("en").NameOf("📁"));
    }

    [Fact]
    public async Task Keywords_are_loaded_once_even_from_two_threads()
    {
        var loads = 0;
        using var start = new Barrier(2);
        var library = new EmojiLibrary(
            new EmojiCatalog([]),
            new SynonymTable([], new Dictionary<string, IReadOnlyList<string>>()),
            code =>
            {
                Interlocked.Increment(ref loads);
                Thread.Sleep(100);
                return EmojiKeywords.Empty(code);
            });

        EmojiKeywords Load()
        {
            start.SignalAndWait();
            return library.Keywords("fr");
        }

        var results = await Task.WhenAll(Task.Run(Load), Task.Run(Load));

        Assert.Equal(1, loads);
        Assert.Same(results[0], results[1]);
        Assert.Same(results[0], library.Keywords("fr"));
        Assert.Equal(1, loads);
    }
}
