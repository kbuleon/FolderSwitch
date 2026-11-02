// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Localization;

namespace FolderSwitch.Core.Tests.Localization;

public class LanguagesTests
{
    private static readonly string[] AllNine = ["en", "fr", "es", "de", "it", "pt-BR", "pt-PT", "nl", "pl"];

    [Fact]
    public void Default_language_is_english()
    {
        Assert.Equal("en", Languages.DefaultCode);
    }

    [Theory]
    [InlineData("fr-FR", "fr")]
    [InlineData("fr-CA", "fr")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-PT")]
    [InlineData("pt-AO", "pt-PT")]
    [InlineData("de-AT", "de")]
    [InlineData("ja-JP", "en")]
    [InlineData("en-GB", "en")]
    public void SelectInitial_follows_the_windows_culture(string windowsCulture, string expected)
    {
        Assert.Equal(expected, Languages.SelectInitial(windowsCulture, AllNine));
    }

    [Theory]
    [InlineData("en-US", "en")]
    [InlineData("es-MX", "es")]
    [InlineData("nl-BE", "nl")]
    [InlineData("pl-PL", "pl")]
    [InlineData("it-CH", "it")]
    [InlineData("pt", "pt-PT")]
    [InlineData("pt-MZ", "pt-PT")]
    [InlineData("zh-Hans-CN", "en")]
    [InlineData("sr-Latn-RS", "en")]
    public void SelectInitial_handles_other_culture_names(string windowsCulture, string expected)
    {
        Assert.Equal(expected, Languages.SelectInitial(windowsCulture, AllNine));
    }

    [Theory]
    [InlineData("FR-fr", "fr")]
    [InlineData("PT-br", "pt-BR")]
    [InlineData("pt-pt", "pt-PT")]
    [InlineData("  de-DE  ", "de")]
    public void SelectInitial_ignores_case_and_surrounding_spaces(string windowsCulture, string expected)
    {
        Assert.Equal(expected, Languages.SelectInitial(windowsCulture, AllNine));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SelectInitial_falls_back_to_english_for_a_blank_culture(string windowsCulture)
    {
        Assert.Equal("en", Languages.SelectInitial(windowsCulture, AllNine));
    }

    [Fact]
    public void SelectInitial_only_returns_available_languages()
    {
        string[] onlyEnglishAndFrench = ["en", "fr"];

        Assert.Equal("fr", Languages.SelectInitial("fr-CA", onlyEnglishAndFrench));
        Assert.Equal("en", Languages.SelectInitial("de-DE", onlyEnglishAndFrench));
        Assert.Equal("en", Languages.SelectInitial("pt-BR", onlyEnglishAndFrench));
    }

    [Fact]
    public void SelectInitial_prefers_an_exact_match_to_the_neutral_language()
    {
        string[] withRegional = ["en", "fr", "fr-CA"];

        Assert.Equal("fr-CA", Languages.SelectInitial("fr-CA", withRegional));
        Assert.Equal("fr", Languages.SelectInitial("fr-FR", withRegional));
    }

    [Fact]
    public void SelectInitial_returns_the_code_as_spelled_in_the_available_list()
    {
        string[] available = ["en", "PT-br"];

        Assert.Equal("PT-br", Languages.SelectInitial("pt-BR", available));
    }

    [Fact]
    public void SelectInitial_sends_other_portuguese_to_the_neighbouring_variant_when_pt_PT_is_missing()
    {
        string[] onlyBrazilian = ["en", "pt-BR"];

        Assert.Equal("pt-BR", Languages.SelectInitial("pt-AO", onlyBrazilian));
        Assert.Equal("pt-BR", Languages.SelectInitial("pt-PT", onlyBrazilian));
    }

    [Fact]
    public void SelectInitial_returns_english_when_nothing_is_available()
    {
        Assert.Equal("en", Languages.SelectInitial("fr-FR", Array.Empty<string>()));
    }

    [Fact]
    public void SelectInitial_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => Languages.SelectInitial("fr-FR", null!));
    }

    [Fact]
    public void SelectInitial_treats_a_null_culture_as_blank()
    {
        Assert.Equal("en", Languages.SelectInitial(null!, AllNine));
    }

    [Theory]
    [InlineData("pt-PT", "pt-PT,pt-BR,en")]
    [InlineData("pt-BR", "pt-BR,pt-PT,en")]
    [InlineData("fr", "fr,en")]
    public void FallbackChain_follows_the_spec(string code, string expectedChain)
    {
        Assert.Equal(expectedChain, string.Join(",", Languages.FallbackChain(code)));
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("xx", "xx,en")]
    [InlineData("de", "de,en")]
    [InlineData("PT-pt", "pt-PT,pt-BR,en")]
    [InlineData("pt-br", "pt-BR,pt-PT,en")]
    [InlineData("", "en")]
    [InlineData("  ", "en")]
    public void FallbackChain_handles_edge_cases(string code, string expectedChain)
    {
        Assert.Equal(expectedChain, string.Join(",", Languages.FallbackChain(code)));
    }

    [Fact]
    public void FallbackChain_always_ends_with_english_without_repeating_it()
    {
        foreach (var code in AllNine)
        {
            var chain = Languages.FallbackChain(code);

            Assert.Equal(code, chain[0]);
            Assert.Equal("en", chain[^1]);
            Assert.Single(chain, c => c == "en");
            Assert.Equal(chain.Count, chain.Distinct().Count());
        }
    }

    [Fact]
    public void FallbackChain_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => Languages.FallbackChain(null!));
    }

    [Fact]
    public void FallbackChain_cannot_be_modified_through_a_cast()
    {
        var chain = Languages.FallbackChain("pt-PT");

        if (chain is IList<string> list)
        {
            Assert.Throws<NotSupportedException>(() => list[0] = "xx");
        }

        Assert.Equal("pt-PT", Languages.FallbackChain("pt-PT")[0]);
    }
}
