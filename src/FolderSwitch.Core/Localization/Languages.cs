// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Localization;

/// <summary>
/// Pure rules about language codes: which language to start with, and which languages answer when a
/// text is missing. The set of languages itself is not known here: it is whatever translation files exist.
/// </summary>
public static class Languages
{
    /// <summary>The reference language, and the last resort of every fallback chain.</summary>
    public const string DefaultCode = "en";

    private const string PortugalCode = "pt-PT";
    private const string BrazilCode = "pt-BR";
    private const string PortugueseLanguage = "pt";

    /// <summary>
    /// The language to start with for a given Windows display language: an exact match (<c>pt-BR</c>,
    /// <c>pt-PT</c>), else the neutral language (<c>fr-CA</c> gives <c>fr</c>), else <c>pt-PT</c> for any other
    /// Portuguese variant, else English. Only codes from <paramref name="available"/> are returned, spelled as
    /// they are there; English is the answer when nothing matches.
    /// </summary>
    /// <param name="windowsUiCultureName">A culture name such as <c>fr-FR</c>; blank means unknown.</param>
    /// <param name="available">The codes of the languages that can actually be loaded.</param>
    public static string SelectInitial(string windowsUiCultureName, IReadOnlyCollection<string> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        if (string.IsNullOrWhiteSpace(windowsUiCultureName))
        {
            return DefaultCode;
        }

        var cultureName = windowsUiCultureName.Trim();

        if (TryFind(available, cultureName, out var exact))
        {
            return exact;
        }

        var dash = cultureName.IndexOf('-');
        var neutral = dash < 0 ? cultureName : cultureName[..dash];
        if (TryFind(available, neutral, out var neutralMatch))
        {
            return neutralMatch;
        }

        if (neutral.Equals(PortugueseLanguage, StringComparison.OrdinalIgnoreCase))
        {
            // Any other Portuguese: the European variant, or its neighbour when that one is not shipped.
            foreach (var candidate in FallbackChain(PortugalCode))
            {
                if (TryFind(available, candidate, out var portuguese))
                {
                    return portuguese;
                }
            }
        }

        return DefaultCode;
    }

    /// <summary>
    /// The languages to consult, in order, for a text missing from <paramref name="code"/>:
    /// <c>pt-PT</c> then <c>pt-BR</c> then English, <c>pt-BR</c> then <c>pt-PT</c> then English, and for any
    /// other language the language itself then English. The chain starts with the language itself and always
    /// ends with English, which appears once.
    /// </summary>
    public static IReadOnlyList<string> FallbackChain(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (string.IsNullOrWhiteSpace(code) || code.Equals(DefaultCode, StringComparison.OrdinalIgnoreCase))
        {
            return [DefaultCode];
        }

        if (code.Equals(PortugalCode, StringComparison.OrdinalIgnoreCase))
        {
            return [PortugalCode, BrazilCode, DefaultCode];
        }

        if (code.Equals(BrazilCode, StringComparison.OrdinalIgnoreCase))
        {
            return [BrazilCode, PortugalCode, DefaultCode];
        }

        return [code, DefaultCode];
    }

    private static bool TryFind(IReadOnlyCollection<string> available, string code, out string found)
    {
        foreach (var candidate in available)
        {
            if (candidate.Equals(code, StringComparison.OrdinalIgnoreCase))
            {
                found = candidate;
                return true;
            }
        }

        found = string.Empty;
        return false;
    }
}
