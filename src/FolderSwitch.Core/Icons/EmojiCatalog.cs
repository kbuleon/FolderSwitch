// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Icons;

/// <summary>
/// The emojis FolderSwitch offers, in catalog order. An emoji is found with or without its U+FE0F (variation
/// selector 16): the keyword data and the configuration may write either form. Immutable, so safe between threads.
/// </summary>
public sealed class EmojiCatalog
{
    private const string VariationSelector16 = "\uFE0F";

    private readonly Dictionary<string, int> _indexByBase = new(StringComparer.Ordinal);

    /// <summary>
    /// A catalog of <paramref name="entries"/>, sorted by <see cref="EmojiEntry.Order"/> (stable). Of two entries for
    /// the same emoji (with or without U+FE0F), the first is kept.
    /// </summary>
    public EmojiCatalog(IEnumerable<EmojiEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var kept = new List<EmojiEntry>();
        foreach (var entry in entries.OrderBy(entry => entry.Order))
        {
            if (_indexByBase.TryAdd(BaseForm(entry.Emoji), kept.Count))
            {
                kept.Add(entry);
            }
        }

        All = kept.AsReadOnly();
    }

    /// <summary>Every emoji, in catalog order.</summary>
    public IReadOnlyList<EmojiEntry> All { get; }

    /// <summary>The entry of <paramref name="emoji"/>, written with or without its final U+FE0F; <c>null</c> when it is not in the catalog.</summary>
    public EmojiEntry? Find(string emoji)
    {
        var index = IndexOf(emoji);
        return index < 0 ? null : All[index];
    }

    /// <summary>Whether <paramref name="emoji"/>, written with or without its final U+FE0F, is in the catalog.</summary>
    public bool Contains(string emoji) => IndexOf(emoji) >= 0;

    /// <summary>The position of <paramref name="emoji"/> in <see cref="All"/>; -1 when it is not in the catalog.</summary>
    internal int IndexOf(string emoji)
    {
        ArgumentNullException.ThrowIfNull(emoji);
        return _indexByBase.TryGetValue(BaseForm(emoji), out var index) ? index : -1;
    }

    /// <summary><paramref name="emoji"/> without any U+FE0F: the form two spellings of the same emoji share.</summary>
    internal static string BaseForm(string emoji) => emoji.Replace(VariationSelector16, string.Empty, StringComparison.Ordinal);
}
