// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Icons;

/// <summary>
/// A concept of FolderSwitch's synonym table: the <paramref name="Emoji"/> chosen for it and the <paramref name="Words"/>
/// that name it, by language code.
/// </summary>
public sealed record SynonymConcept(string Emoji, IReadOnlyDictionary<string, IReadOnlyList<string>> Words);

/// <summary>
/// FolderSwitch's own table of common folder and site words, each leading to a chosen emoji, plus the stop words of
/// each language (ignored in names). Immutable, so safe between threads.
/// </summary>
public sealed class SynonymTable
{
    private static readonly IReadOnlySet<string> NoStopWords = new HashSet<string>(StringComparer.Ordinal);

    private readonly Dictionary<string, IReadOnlySet<string>> _stopWords = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The table of <paramref name="concepts"/>, with the <paramref name="stopWords"/> of each language code.</summary>
    public SynonymTable(IEnumerable<SynonymConcept> concepts, IReadOnlyDictionary<string, IReadOnlyList<string>> stopWords)
    {
        ArgumentNullException.ThrowIfNull(concepts);
        ArgumentNullException.ThrowIfNull(stopWords);

        Concepts = concepts.ToList().AsReadOnly();
        foreach (var (code, words) in stopWords)
        {
            _stopWords[code] = words.Select(IconWords.Normalize).ToHashSet(StringComparer.Ordinal);
        }
    }

    /// <summary>Every concept, in table order.</summary>
    public IReadOnlyList<SynonymConcept> Concepts { get; }

    /// <summary>The stop words of language <paramref name="code"/>, normalized by <see cref="IconWords.Normalize"/>; empty for a language without any.</summary>
    public IReadOnlySet<string> StopWords(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _stopWords.GetValueOrDefault(code) ?? NoStopWords;
    }
}
