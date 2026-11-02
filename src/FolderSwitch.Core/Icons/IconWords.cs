// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.Text;

namespace FolderSwitch.Core.Icons;

/// <summary>Cuts a name, a keyword or a typed filter into comparable words: lowered, without accents, singular.</summary>
public static class IconWords
{
    /// <summary>
    /// <paramref name="word"/> in invariant lower case, its diacritics removed (« École » → « ecole »). A text that is
    /// not valid Unicode (a lone surrogate) cannot be decomposed: it is only lowered.
    /// </summary>
    public static string Normalize(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        var lowered = word.ToLowerInvariant();
        string decomposed;
        try
        {
            decomposed = lowered.Normalize(NormalizationForm.FormD);
        }
        catch (ArgumentException)
        {
            return lowered;
        }

        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// The normalized words of <paramref name="text"/>, in order: cut at every character that is neither a letter nor a
    /// digit (spaces, apostrophes, dashes, punctuation, emoji), without the <paramref name="stopWords"/> (normalized
    /// words) and without the words of one letter.
    /// </summary>
    public static IReadOnlyList<string> Split(string text, IReadOnlySet<string> stopWords)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(stopWords);

        var normalized = Normalize(text);
        var words = new List<string>();
        var start = 0;
        var letters = 0;
        var position = 0;
        while (position < normalized.Length)
        {
            Rune.DecodeFromUtf16(normalized.AsSpan(position), out var rune, out var length);
            if (Rune.IsLetterOrDigit(rune))
            {
                letters++;
            }
            else
            {
                Keep(start, position, letters);
                start = position + length;
                letters = 0;
            }

            position += length;
        }

        Keep(start, normalized.Length, letters);
        return words;

        void Keep(int from, int to, int count)
        {
            if (count > 1 && normalized[from..to] is var word && !stopWords.Contains(word))
            {
                words.Add(word);
            }
        }
    }

    /// <summary>
    /// A simple singular of a normalized <paramref name="word"/>: without its final <c>s</c> or <c>x</c> when it has more
    /// than 3 letters (« finances » → « finance »); otherwise unchanged (« gas »).
    /// </summary>
    public static string Singular(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        return word.Length > 3 && word[^1] is 's' or 'x' ? word[..^1] : word;
    }
}
