// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using System.Text.Json;
using FolderSwitch.Core.Icons;

namespace FolderSwitch.Core.Tests.Icons;

/// <summary>Checks on the synonym table shipped by the application (synonyms.json).</summary>
public class SynonymFileTests
{
    private static readonly IReadOnlySet<string> NoStopWords = new HashSet<string>(StringComparer.Ordinal);

    private static IReadOnlyList<SynonymConcept> Concepts => ShippedEmojiData.Library.Synonyms.Concepts;

    [Fact]
    public void Every_concept_has_words_in_nine_languages()
    {
        // Read strictly, so that an entry the lenient loader would skip fails here.
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ShippedEmojiData.EmojiDirectory, "synonyms.json"), Encoding.UTF8));
        var raw = document.RootElement.GetProperty("concepts").EnumerateArray().ToList();

        Assert.True(raw.Count >= 100, $"Only {raw.Count} concepts.");
        Assert.Equal(raw.Count, Concepts.Count);

        foreach (var concept in raw)
        {
            var emoji = concept.GetProperty("emoji").GetString();
            Assert.False(string.IsNullOrWhiteSpace(emoji));

            var languages = concept.GetProperty("words").EnumerateObject().ToList();
            Assert.Equal(
                ShippedEmojiData.Codes.Order(StringComparer.Ordinal),
                languages.Select(language => language.Name).Order(StringComparer.Ordinal));

            foreach (var language in languages)
            {
                var words = language.Value.EnumerateArray().ToList();
                Assert.True(words.Count > 0, $"{emoji} has no word in {language.Name}.");
                Assert.All(words, word => Assert.False(
                    string.IsNullOrWhiteSpace(word.GetString()),
                    $"{emoji} has a blank word in {language.Name}."));
            }
        }
    }

    [Fact]
    public void Every_synonym_is_one_word()
    {
        // A name is cut into words before the lookup: a synonym of several words, or with a dash, never matches.
        foreach (var concept in Concepts)
        {
            foreach (var (code, words) in concept.Words)
            {
                foreach (var word in words)
                {
                    Assert.True(
                        IconWords.Split(word, NoStopWords) is [var only] && only == IconWords.Normalize(word),
                        $"'{word}' ({code}, {concept.Emoji}) is not one word.");
                }
            }
        }
    }

    [Fact]
    public void Every_synonym_emoji_is_in_the_catalog()
    {
        var catalog = ShippedEmojiData.Library.Catalog;
        Assert.All(Concepts, concept => Assert.True(catalog.Contains(concept.Emoji), $"{concept.Emoji} is not in the catalog."));
    }

    [Fact]
    public void Every_language_has_stop_words()
    {
        var synonyms = ShippedEmojiData.Library.Synonyms;
        foreach (var code in ShippedEmojiData.Codes)
        {
            var stopWords = synonyms.StopWords(code);
            Assert.True(stopWords.Count > 0, $"{code} has no stop word.");

            // A synonym that is also a stop word of its language is never reached in that language.
            var words = Concepts
                .SelectMany(concept => concept.Words.GetValueOrDefault(code) ?? [])
                .Select(IconWords.Normalize);
            Assert.All(words, word => Assert.False(stopWords.Contains(word), $"'{word}' ({code}) is both a synonym and a stop word."));
        }
    }

    [Fact]
    public void No_word_leads_to_two_concepts_in_one_language()
    {
        foreach (var code in ShippedEmojiData.Codes)
        {
            // Each word counts with its singular too, as the finder looks them both up.
            var conceptsByForm = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            for (var index = 0; index < Concepts.Count; index++)
            {
                foreach (var word in Concepts[index].Words.GetValueOrDefault(code) ?? [])
                {
                    var normalized = IconWords.Normalize(word);
                    foreach (var form in new[] { normalized, IconWords.Singular(normalized) })
                    {
                        if (!conceptsByForm.TryGetValue(form, out var concepts))
                        {
                            conceptsByForm[form] = concepts = [];
                        }

                        concepts.Add(index);
                    }
                }
            }

            var shared = conceptsByForm
                .Where(pair => pair.Value.Count > 1)
                .Select(pair => $"'{pair.Key}' → {string.Join(' ', pair.Value.Select(index => Concepts[index].Emoji))}")
                .ToList();
            Assert.True(shared.Count == 0, $"{code}: {string.Join(", ", shared)}");
        }
    }
}
