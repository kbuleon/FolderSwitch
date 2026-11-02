// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Icons;

namespace FolderSwitch.Core.Tests.Icons;

public class IconWordsTests
{
    private static readonly HashSet<string> FrenchStopWords = ["de", "la", "le", "et", "les"];

    private static readonly HashSet<string> NoStopWords = [];

    [Fact]
    public void Split_lowers_strips_accents_and_drops_stop_words()
    {
        Assert.Equal(["films", "series"], IconWords.Split("Films et séries", FrenchStopWords));
    }

    [Fact]
    public void Split_cuts_at_apostrophes_and_drops_one_letter_words()
    {
        Assert.Equal(["atelier"], IconWords.Split("L'atelier", FrenchStopWords));
    }

    [Fact]
    public void Split_keeps_numbers()
    {
        Assert.Equal(["2026"], IconWords.Split("2026", NoStopWords));
    }

    [Fact]
    public void Split_finds_no_word_in_symbols_and_emoji()
    {
        Assert.Empty(IconWords.Split("--- 💰 ---", NoStopWords));
    }

    [Fact]
    public void Split_cuts_at_dashes_and_punctuation()
    {
        Assert.Equal(["mots", "passe", "perso"], IconWords.Split("Mots-de-passe (perso)", FrenchStopWords));
    }

    [Fact]
    public void Singular_drops_a_final_s_or_x_of_long_words()
    {
        Assert.Equal("finance", IconWords.Singular("finances"));
        Assert.Equal("bijou", IconWords.Singular("bijoux"));
    }

    [Fact]
    public void Singular_leaves_short_words_alone()
    {
        Assert.Equal("gas", IconWords.Singular("gas"));
        Assert.Equal("code", IconWords.Singular("code"));
    }

    [Fact]
    public void Normalize_lowers_and_strips_accents()
    {
        Assert.Equal("ecole", IconWords.Normalize("École"));
        Assert.Equal("telechargements", IconWords.Normalize("Téléchargements"));
    }

    [Fact]
    public void A_lone_surrogate_does_not_throw()
    {
        Assert.Equal("a\uD83D b", IconWords.Normalize("A\uD83D B"));
        Assert.Equal(["film"], IconWords.Split("film \uD83D", NoStopWords));
    }
}
