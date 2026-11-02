// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.Concurrent;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Core.Icons;

/// <summary>
/// Finds emojis from words: the automatic icon of a tab or group name (<see cref="Suggest"/>) and the emojis matching
/// the text typed in the picker (<see cref="Filter"/>). Only offers what the machine's font can draw. The word indexes
/// are built the first time they are needed (one for the synonyms, one per language for the CLDR keywords and for the
/// filter) and kept. Safe between threads.
/// </summary>
public sealed class IconFinder
{
    private const string English = "en";

    // Scores of the ranking of Suggest: a synonym always beats a CLDR keyword.
    private const int SynonymCurrentLanguage = 1000;
    private const int SynonymEnglish = 900;
    private const int SynonymOtherLanguage = 800;
    private const int WholeKeyword = 500;
    private const int WordOfKeyword = 300;
    private const int WordOfName = 200;
    private const int EnglishKeywordPenalty = 50;
    private const int WordPositionPenalty = 10;
    private const int CharacterPenalty = 100;

    private static readonly IReadOnlySet<string> NoStopWords = new HashSet<string>(StringComparer.Ordinal);

    private readonly EmojiLibrary _library;
    private readonly IEmojiGlyphs _glyphs;
    private readonly Lazy<bool[]> _drawable;
    private readonly Lazy<IReadOnlyList<EmojiEntry>> _drawableList;
    private readonly Lazy<Dictionary<string, List<SynonymHit>>> _synonyms;
    private readonly ConcurrentDictionary<string, Lazy<Dictionary<string, Dictionary<int, int>>>> _keywordIndexes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Lazy<HashSet<string>[]>> _filterIndexes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A finder over the emojis of <paramref name="library"/> that <paramref name="glyphs"/> can draw.</summary>
    public IconFinder(EmojiLibrary library, IEmojiGlyphs glyphs)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(glyphs);

        _library = library;
        _glyphs = glyphs;
        _drawable = new Lazy<bool[]>(() => _library.Catalog.All.Select(entry => _glyphs.CanDraw(entry.Emoji)).ToArray());
        _drawableList = new Lazy<IReadOnlyList<EmojiEntry>>(
            () => _library.Catalog.All.Where((_, index) => _drawable.Value[index]).ToList().AsReadOnly());
        _synonyms = new Lazy<Dictionary<string, List<SynonymHit>>>(BuildSynonymIndex);
    }

    /// <summary>The catalog emojis the font can draw, in catalog order; asked to the font once.</summary>
    public IReadOnlyList<EmojiEntry> Drawable => _drawableList.Value;

    /// <summary>Whether <paramref name="emoji"/> (with or without its U+FE0F) is in the catalog and the font can draw it.</summary>
    public bool CanShow(string? emoji)
    {
        if (string.IsNullOrEmpty(emoji))
        {
            return false;
        }

        var index = _library.Catalog.IndexOf(emoji);
        return index >= 0 && _drawable.Value[index];
    }

    /// <summary>The CLDR name of <paramref name="emoji"/> in language <paramref name="languageCode"/>, else in English; <c>null</c> when neither has one.</summary>
    public string? NameOf(string emoji, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(emoji);
        ArgumentNullException.ThrowIfNull(languageCode);

        return _library.Keywords(languageCode).NameOf(emoji) ?? _library.Keywords(English).NameOf(emoji);
    }

    /// <summary>
    /// The emoji for a tab or group <paramref name="name"/>, as the catalog writes it, or <c>null</c> when no word of the
    /// name leads to a drawable emoji. Each word (without the stop words of the language and of English), and its
    /// singular, is looked up in the synonym table of every language (the current language first, then English, then
    /// the others), then in the CLDR keywords of the current language and of English (a whole keyword first, then a
    /// word of a keyword, then a word of the emoji's name). An earlier word wins a tie, then the catalog order; among
    /// CLDR keywords, objects, places and symbols are preferred to smileys and people.
    /// </summary>
    public string? Suggest(string name, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(languageCode);

        var stopWords = new HashSet<string>(_library.Synonyms.StopWords(languageCode), StringComparer.Ordinal);
        stopWords.UnionWith(_library.Synonyms.StopWords(English));
        var words = IconWords.Split(name, stopWords);
        if (words.Count == 0)
        {
            return null;
        }

        var keywordLanguages = IsEnglish(languageCode)
            ? new[] { (Index: KeywordIndex(English), Penalty: 0) }
            : [(KeywordIndex(languageCode), 0), (KeywordIndex(English), EnglishKeywordPenalty)];
        var all = _library.Catalog.All;
        var scores = new Dictionary<int, int>();

        for (var position = 0; position < words.Count; position++)
        {
            var positionPenalty = WordPositionPenalty * position;
            foreach (var form in Forms(words[position]))
            {
                if (_synonyms.Value.TryGetValue(form, out var concepts))
                {
                    foreach (var hit in concepts)
                    {
                        Raise(scores, hit.Emoji, SynonymScore(hit.Code, languageCode) - positionPenalty);
                    }
                }

                foreach (var (index, languagePenalty) in keywordLanguages)
                {
                    if (index.TryGetValue(form, out var emojis))
                    {
                        foreach (var (emoji, score) in emojis)
                        {
                            var characterPenalty = all[emoji].Category is EmojiCategory.SmileysEmotion or EmojiCategory.PeopleBody
                                ? CharacterPenalty
                                : 0;
                            Raise(scores, emoji, score - languagePenalty - positionPenalty - characterPenalty);
                        }
                    }
                }
            }
        }

        var best = -1;
        foreach (var (emoji, score) in scores)
        {
            if (_drawable.Value[emoji] && (best < 0 || score > scores[best] || (score == scores[best] && emoji < best)))
            {
                best = emoji;
            }
        }

        return best < 0 ? null : all[best].Emoji;
    }

    /// <summary>
    /// The drawable emojis matching the <paramref name="text"/> typed in the picker: every typed word (or its singular)
    /// must start a word of the emoji's CLDR keywords or name (in language <paramref name="languageCode"/> and in
    /// English) or of its synonyms (same languages). Ranked as the automatic icon is: the emojis where a typed word is a
    /// whole word come first; within each of those two bands, the objects, places, animals and symbols come before the
    /// characters (smileys, people); then catalog order. A blank text, or one without any word, gives
    /// <see cref="Drawable"/>.
    /// </summary>
    public IReadOnlyList<EmojiEntry> Filter(string text, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(languageCode);

        var typed = IconWords.Split(text, NoStopWords).Select(Forms).ToList();
        if (typed.Count == 0)
        {
            return Drawable;
        }

        var index = FilterIndex(languageCode);
        var all = _library.Catalog.All;
        // Four bands, in this order: a whole word, then a word start; in each, objects, places, animals and symbols
        // before the characters (smileys, people).
        var bands = new[] { new List<EmojiEntry>(), new List<EmojiEntry>(), new List<EmojiEntry>(), new List<EmojiEntry>() };
        for (var emoji = 0; emoji < all.Count; emoji++)
        {
            if (!_drawable.Value[emoji])
            {
                continue;
            }

            var words = index[emoji];
            var everyWord = typed.TrueForAll(forms => words.Any(word => forms.Any(form => word.StartsWith(form, StringComparison.Ordinal))));
            if (everyWord)
            {
                var whole = typed.Exists(forms => forms.Any(words.Contains));
                var character = all[emoji].Category is EmojiCategory.SmileysEmotion or EmojiCategory.PeopleBody;
                bands[(whole ? 0 : 2) + (character ? 1 : 0)].Add(all[emoji]);
            }
        }

        return bands.SelectMany(band => band).ToList().AsReadOnly();
    }

    private static bool IsEnglish(string code) => code.Equals(English, StringComparison.OrdinalIgnoreCase);

    /// <summary>A normalized word and its singular, when it differs.</summary>
    private static string[] Forms(string word) =>
        IconWords.Singular(word) is var singular && singular != word ? [word, singular] : [word];

    private static int SynonymScore(string conceptLanguage, string languageCode) =>
        conceptLanguage.Equals(languageCode, StringComparison.OrdinalIgnoreCase) ? SynonymCurrentLanguage
        : IsEnglish(conceptLanguage) ? SynonymEnglish
        : SynonymOtherLanguage;

    private static void Raise(Dictionary<int, int> scores, int emoji, int score)
    {
        if (!scores.TryGetValue(emoji, out var current) || score > current)
        {
            scores[emoji] = score;
        }
    }

    /// <summary>Every synonym word of every language (normalized, and its singular) → the catalog emojis it leads to, with the word's language.</summary>
    private Dictionary<string, List<SynonymHit>> BuildSynonymIndex()
    {
        var index = new Dictionary<string, List<SynonymHit>>(StringComparer.Ordinal);
        foreach (var concept in _library.Synonyms.Concepts)
        {
            var emoji = _library.Catalog.IndexOf(concept.Emoji);
            if (emoji < 0)
            {
                continue;
            }

            foreach (var (code, words) in concept.Words)
            {
                foreach (var word in words)
                {
                    foreach (var form in Forms(IconWords.Normalize(word)))
                    {
                        if (!index.TryGetValue(form, out var hits))
                        {
                            index[form] = hits = [];
                        }

                        hits.Add(new SynonymHit(emoji, code));
                    }
                }
            }
        }

        return index;
    }

    /// <summary>The CLDR word index of a language: each word (and its singular) → the catalog emojis it leads to, with their best score.</summary>
    private Dictionary<string, Dictionary<int, int>> KeywordIndex(string code) =>
        _keywordIndexes.GetOrAdd(
            code,
            static (key, finder) => new Lazy<Dictionary<string, Dictionary<int, int>>>(() => finder.BuildKeywordIndex(key)),
            this).Value;

    private Dictionary<string, Dictionary<int, int>> BuildKeywordIndex(string code)
    {
        var keywords = _library.Keywords(code);
        var index = new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);
        var all = _library.Catalog.All;
        for (var emoji = 0; emoji < all.Count; emoji++)
        {
            foreach (var keyword in keywords.KeywordsOf(all[emoji].Emoji))
            {
                var words = IconWords.Split(keyword, NoStopWords);
                foreach (var word in words)
                {
                    Add(word, emoji, words.Count == 1 ? WholeKeyword : WordOfKeyword);
                }
            }

            foreach (var word in IconWords.Split(keywords.NameOf(all[emoji].Emoji) ?? string.Empty, NoStopWords))
            {
                Add(word, emoji, WordOfName);
            }
        }

        return index;

        void Add(string word, int emoji, int score)
        {
            foreach (var form in Forms(word))
            {
                if (!index.TryGetValue(form, out var emojis))
                {
                    index[form] = emojis = [];
                }

                Raise(emojis, emoji, score);
            }
        }
    }

    /// <summary>The filter index of a language: for each catalog emoji, the words of its keywords, name and synonyms in that language and in English.</summary>
    private HashSet<string>[] FilterIndex(string code) =>
        _filterIndexes.GetOrAdd(
            code,
            static (key, finder) => new Lazy<HashSet<string>[]>(() => finder.BuildFilterIndex(key)),
            this).Value;

    private HashSet<string>[] BuildFilterIndex(string code)
    {
        var languages = IsEnglish(code) ? new[] { English } : [code, English];
        var all = _library.Catalog.All;
        var index = all.Select(_ => new HashSet<string>(StringComparer.Ordinal)).ToArray();

        foreach (var language in languages)
        {
            var keywords = _library.Keywords(language);
            for (var emoji = 0; emoji < all.Count; emoji++)
            {
                foreach (var text in keywords.KeywordsOf(all[emoji].Emoji).Append(keywords.NameOf(all[emoji].Emoji) ?? string.Empty))
                {
                    index[emoji].UnionWith(IconWords.Split(text, NoStopWords));
                }
            }
        }

        foreach (var concept in _library.Synonyms.Concepts)
        {
            var emoji = _library.Catalog.IndexOf(concept.Emoji);
            if (emoji < 0)
            {
                continue;
            }

            foreach (var (language, words) in concept.Words)
            {
                if (languages.Contains(language, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var word in words)
                    {
                        index[emoji].UnionWith(IconWords.Split(word, NoStopWords));
                    }
                }
            }
        }

        return index;
    }

    /// <summary>A synonym word leads to the catalog emoji at <paramref name="Emoji"/>; the word is of language <paramref name="Code"/>.</summary>
    private readonly record struct SynonymHit(int Emoji, string Code);
}
