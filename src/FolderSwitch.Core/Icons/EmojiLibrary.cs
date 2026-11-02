// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.Concurrent;
using System.Text.Json;

namespace FolderSwitch.Core.Icons;

/// <summary>
/// The emoji data FolderSwitch ships: the catalog, the synonym table, and the CLDR keywords of each language, loaded
/// the first time a language is asked for and kept. Safe between threads.
/// </summary>
public sealed class EmojiLibrary
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly JsonDocumentOptions LenientJson = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Dictionary<string, EmojiCategory> CategoriesByName =
        Enum.GetValues<EmojiCategory>().ToDictionary(category => category.ToString(), StringComparer.OrdinalIgnoreCase);

    private readonly Func<string, EmojiKeywords> _loadKeywords;
    private readonly ConcurrentDictionary<string, Lazy<EmojiKeywords>> _keywords = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A library of <paramref name="catalog"/> and <paramref name="synonyms"/>, whose keywords of a language are given by
    /// <paramref name="keywords"/> (called at most once per language code, whatever its case).
    /// </summary>
    public EmojiLibrary(EmojiCatalog catalog, SynonymTable synonyms, Func<string, EmojiKeywords> keywords)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(synonyms);
        ArgumentNullException.ThrowIfNull(keywords);

        Catalog = catalog;
        Synonyms = synonyms;
        _loadKeywords = keywords;
    }

    /// <summary>The emojis offered, in catalog order.</summary>
    public EmojiCatalog Catalog { get; }

    /// <summary>FolderSwitch's synonym table and stop words.</summary>
    public SynonymTable Synonyms { get; }

    /// <summary>
    /// The CLDR names and keywords of language <paramref name="code"/>: loaded on the first call, by one thread even when
    /// several ask at once, then kept.
    /// </summary>
    public EmojiKeywords Keywords(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _keywords.GetOrAdd(
            code,
            static (key, load) => new Lazy<EmojiKeywords>(() => load(key), LazyThreadSafetyMode.ExecutionAndPublication),
            _loadKeywords).Value;
    }

    /// <summary>
    /// The library of <paramref name="dir"/>: <c>catalog.json</c> and <c>synonyms.json</c> read now,
    /// <c>keywords/&lt;code&gt;.json</c> on the first request for that language. Never throws for a missing directory or
    /// a bad file: a missing, unreadable or malformed file gives an empty catalog, an empty table or
    /// <see cref="EmojiKeywords.Empty"/>, and a malformed entry is skipped.
    /// </summary>
    public static EmojiLibrary LoadFromDirectory(string dir)
    {
        ArgumentNullException.ThrowIfNull(dir);

        return new EmojiLibrary(
            ReadCatalog(Path.Combine(dir, "catalog.json")),
            ReadSynonyms(Path.Combine(dir, "synonyms.json")),
            code => ReadKeywords(dir, code));
    }

    private static EmojiCatalog ReadCatalog(string path)
    {
        var entries = new List<EmojiEntry>();
        var complete = TryRead(path, root =>
        {
            foreach (var item in ArrayOf(root, "emoji"))
            {
                if (StringOf(item, "e") is { } emoji
                    && StringOf(item, "c") is { } name
                    && CategoriesByName.TryGetValue(name, out var category))
                {
                    entries.Add(new EmojiEntry(emoji, category, entries.Count));
                }
            }
        });

        return new EmojiCatalog(complete ? entries : []);
    }

    private static SynonymTable ReadSynonyms(string path)
    {
        var concepts = new List<SynonymConcept>();
        var stopWords = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var complete = TryRead(path, root =>
        {
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("stopWords", out var byCode)
                && byCode.ValueKind == JsonValueKind.Object)
            {
                foreach (var language in byCode.EnumerateObject())
                {
                    stopWords[language.Name] = Strings(language.Value);
                }
            }

            foreach (var item in ArrayOf(root, "concepts"))
            {
                if (StringOf(item, "emoji") is { } emoji
                    && item.TryGetProperty("words", out var words)
                    && words.ValueKind == JsonValueKind.Object)
                {
                    var byLanguage = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
                    foreach (var language in words.EnumerateObject())
                    {
                        byLanguage[language.Name] = Strings(language.Value);
                    }

                    concepts.Add(new SynonymConcept(emoji, byLanguage));
                }
            }
        });

        return complete
            ? new SynonymTable(concepts, stopWords)
            : new SynonymTable([], new Dictionary<string, IReadOnlyList<string>>());
    }

    private static EmojiKeywords ReadKeywords(string dir, string code)
    {
        // The code becomes a file name: anything but a plain language code (« pt-BR ») has no file.
        if (code.Length == 0 || !code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            return EmojiKeywords.Empty(code);
        }

        var byEmoji = new Dictionary<string, (string Name, IReadOnlyList<string> Keywords)>(StringComparer.Ordinal);
        var complete = TryRead(Path.Combine(dir, "keywords", code + ".json"), root =>
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("emoji", out var emojis)
                || emojis.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var emoji in emojis.EnumerateObject())
            {
                if (emoji.Value.ValueKind == JsonValueKind.Object)
                {
                    var keywords = emoji.Value.TryGetProperty("keywords", out var list) ? Strings(list) : [];
                    byEmoji[emoji.Name] = (StringOf(emoji.Value, "name") ?? string.Empty, keywords);
                }
            }
        });

        return complete ? new EmojiKeywords(code, byEmoji) : EmojiKeywords.Empty(code);
    }

    /// <summary>Parses <paramref name="path"/> and hands its root to <paramref name="read"/>; <c>false</c> when the file is missing, unreadable or malformed.</summary>
    private static bool TryRead(string path, Action<JsonElement> read)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            // A UTF-8 byte-order mark is common in files saved by hand; the JSON parser does not accept it.
            var bytes = File.ReadAllBytes(path).AsMemory();
            if (bytes.Span.StartsWith(Utf8Bom))
            {
                bytes = bytes[Utf8Bom.Length..];
            }

            using var document = JsonDocument.Parse(bytes, LenientJson);
            read(document.RootElement);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            // Emoji data must never stop the application: without it, there are simply no icons to offer.
            // InvalidOperationException: bytes that are not valid UTF-8 only fail when a text is read.
            return false;
        }
    }

    private static IEnumerable<JsonElement> ArrayOf(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(name, out var array)
        && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    private static string? StringOf(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static string[] Strings(JsonElement array) =>
        array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray()
            : [];
}
