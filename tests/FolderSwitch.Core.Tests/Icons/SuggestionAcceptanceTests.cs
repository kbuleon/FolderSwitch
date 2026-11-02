// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using System.Text.Json;
using FolderSwitch.Core.Icons;

namespace FolderSwitch.Core.Tests.Icons;

/// <summary>
/// The automatic icon given by the shipped data to the names of a real board (the README example, common folder
/// names), the application being in French or in English.
/// </summary>
public class SuggestionAcceptanceTests
{
    private static readonly Lazy<IconFinder> SharedFinder = new(ShippedEmojiData.Finder);

    public static readonly TheoryData<string> FrenchNames =
    [
        "Travail", "Maison", "Admin", "Projets", "Clients", "Personnel", "Archives", "Finance", "Finances", "Outils",
        "Réseau", "Photos", "Films et séries", "Autres dossiers Windows", "Documents", "Téléchargements", "Musique",
        "Vidéos", "Jeux", "Développement", "Factures", "Banque", "Impôts", "Voyages", "Famille", "École", "Santé",
        "Sport", "Cuisine", "Recettes", "Code", "Scripts", "Logs", "Serveurs", "Bureau", "Perso", "Pro",
        "Mes dossiers", "Windows",
    ];

    public static readonly TheoryData<string> EnglishNames =
    [
        "Work", "Home", "Admin", "Projects", "Clients", "Personal", "Archives", "Finance", "Finances", "Tools",
        "Network", "Photos", "Movies and series", "Other Windows folders", "Documents", "Downloads", "Music",
        "Videos", "Games", "Development", "Invoices", "Bank", "Taxes", "Travel", "Family", "School", "Health",
        "Sport", "Cooking", "Recipes", "Code", "Scripts", "Logs", "Servers", "Desktop", "Private", "Pro",
        "My folders", "Windows",
    ];

    public static readonly TheoryData<string, string> FixedFrench = new()
    {
        { "Finance", "💵" },
        { "Maison", "🏠" },
        { "Projets", "🚀" },
        { "Outils", "🧰" },
        { "Clients", "👥" },
        { "Photos", "📷" },
        { "Musique", "🎵" },
    };

    public static readonly TheoryData<string, string> FixedEnglish = new()
    {
        { "Finance", "💵" },
        { "Home", "🏠" },
        { "Projects", "🚀" },
        { "Tools", "🧰" },
        { "Clients", "👥" },
        { "Photos", "📷" },
        { "Music", "🎵" },
    };

    public static readonly TheoryData<string> NamesWithoutIcon =
    [
        "Dropbox", "FolderSwitch", "UC Track", "Acme", "Nouveau groupe", "Nouvel onglet", "New tab", "New group",
    ];

    public static readonly TheoryData<string> Codes = [.. ShippedEmojiData.Codes];

    // Plurals that the simple singular does not reduce to a listed word.
    public static readonly TheoryData<string, string, string> FixedOtherLanguages = new()
    {
        { "it", "Tasse", "🏛️" },
        { "pt-PT", "Ficheiros", "📁" },
    };

    private static IconFinder Finder => SharedFinder.Value;

    [Theory]
    [MemberData(nameof(FixedOtherLanguages))]
    public void Names_get_their_chosen_icon_in_their_language(string code, string name, string emoji) =>
        Assert.Equal(Bare(emoji), Bare(Finder.Suggest(name, code)));

    [Theory]
    [MemberData(nameof(FrenchNames))]
    public void French_names_get_an_icon_in_french(string name) =>
        Assert.NotNull(Finder.Suggest(name, "fr"));

    [Theory]
    [MemberData(nameof(FrenchNames))]
    public void French_names_get_an_icon_in_english(string name) =>
        Assert.NotNull(Finder.Suggest(name, "en"));

    [Theory]
    [MemberData(nameof(EnglishNames))]
    public void English_names_get_an_icon_in_english(string name) =>
        Assert.NotNull(Finder.Suggest(name, "en"));

    [Theory]
    [MemberData(nameof(FixedFrench))]
    public void French_names_get_their_chosen_icon_in_french(string name, string emoji) =>
        Assert.Equal(emoji, Bare(Finder.Suggest(name, "fr")));

    [Theory]
    [MemberData(nameof(FixedFrench))]
    public void French_names_get_their_chosen_icon_in_english(string name, string emoji) =>
        Assert.Equal(emoji, Bare(Finder.Suggest(name, "en")));

    [Theory]
    [MemberData(nameof(FixedEnglish))]
    public void English_names_get_their_chosen_icon_in_english(string name, string emoji) =>
        Assert.Equal(emoji, Bare(Finder.Suggest(name, "en")));

    [Theory]
    [MemberData(nameof(NamesWithoutIcon))]
    public void Proper_and_default_names_get_no_icon_in_french(string name) =>
        Assert.Null(Finder.Suggest(name, "fr"));

    [Theory]
    [MemberData(nameof(NamesWithoutIcon))]
    public void Proper_and_default_names_get_no_icon_in_english(string name) =>
        Assert.Null(Finder.Suggest(name, "en"));

    [Theory]
    [MemberData(nameof(Codes))]
    public void Default_names_get_no_icon_in_their_language(string code)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(ShippedEmojiData.StringsDirectory, code + ".json"), Encoding.UTF8));
        foreach (var key in new[] { "Tab.New", "Group.New" })
        {
            var name = document.RootElement.GetProperty(key).GetString()!;
            Assert.True(Finder.Suggest(name, code) is null, $"'{name}' ({code}) gets {Finder.Suggest(name, code)}.");
        }
    }

    [Fact]
    public void The_picker_filter_puts_the_cat_before_the_cat_smiley()
    {
        var results = Finder.Filter("chat", "fr").Select(entry => Bare(entry.Emoji)).ToList();
        Assert.True(results.Count > 0 && results[0] == "🐱", string.Join(" ", results));
    }

    private static string? Bare(string? emoji) => emoji?.Replace("\uFE0F", string.Empty, StringComparison.Ordinal);
}
