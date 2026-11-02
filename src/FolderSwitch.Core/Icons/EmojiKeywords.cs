// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Icons;

/// <summary>
/// The Unicode (CLDR) names and keywords of the emojis in one language, as written in the data (neither lowered nor
/// stripped of accents). An emoji is found with or without its U+FE0F. Immutable, so safe between threads.
/// </summary>
public sealed class EmojiKeywords
{
    private readonly Dictionary<string, (string Name, IReadOnlyList<string> Keywords)> _byBase = new(StringComparer.Ordinal);

    /// <summary>The names and keywords of language <paramref name="code"/>, by emoji (either form).</summary>
    public EmojiKeywords(string code, IReadOnlyDictionary<string, (string Name, IReadOnlyList<string> Keywords)> byEmoji)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(byEmoji);

        Code = code;
        foreach (var (emoji, data) in byEmoji)
        {
            _byBase.TryAdd(EmojiCatalog.BaseForm(emoji), data);
        }
    }

    /// <summary>The language code these keywords are for (<c>fr</c>, <c>pt-BR</c>…).</summary>
    public string Code { get; }

    /// <summary>No name and no keyword, for a language without data.</summary>
    public static EmojiKeywords Empty(string code) =>
        new(code, new Dictionary<string, (string Name, IReadOnlyList<string> Keywords)>());

    /// <summary>The name of <paramref name="emoji"/> in this language; <c>null</c> when the data has none.</summary>
    public string? NameOf(string emoji)
    {
        ArgumentNullException.ThrowIfNull(emoji);
        return _byBase.TryGetValue(EmojiCatalog.BaseForm(emoji), out var data) && !string.IsNullOrWhiteSpace(data.Name)
            ? data.Name
            : null;
    }

    /// <summary>The keywords of <paramref name="emoji"/> in this language; empty when the data has none.</summary>
    public IReadOnlyList<string> KeywordsOf(string emoji)
    {
        ArgumentNullException.ThrowIfNull(emoji);
        return _byBase.TryGetValue(EmojiCatalog.BaseForm(emoji), out var data) ? data.Keywords : [];
    }
}
