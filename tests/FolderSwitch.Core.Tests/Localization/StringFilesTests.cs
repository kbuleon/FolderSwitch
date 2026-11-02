// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.SpecialFolders;

namespace FolderSwitch.Core.Tests.Localization;

/// <summary>Checks on the real translation files shipped by the application.</summary>
public class StringFilesTests
{
    private static readonly string[] ExpectedCodes = ["en", "de", "es", "fr", "it", "nl", "pl", "pt-BR", "pt-PT"];

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Lazy<string> StringsDirectory = new(LocateStringsDirectory);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Files = new(LoadAll);

    private static string LocateStringsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FolderSwitch.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("FolderSwitch.slnx was not found above " + AppContext.BaseDirectory);
        }

        return Path.Combine(directory.FullName, "src", "FolderSwitch.App", "Strings");
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LoadAll()
    {
        var files = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(StringsDirectory.Value, "*.json"))
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            string text;
            try
            {
                text = StrictUtf8.GetString(File.ReadAllBytes(path));
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException($"{path} is not valid UTF-8 (saved as ANSI?).", exception);
            }

            using var document = JsonDocument.Parse(text);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                entries.Add(property.Name, property.Value.GetString() ?? string.Empty);
            }

            files.Add(Path.GetFileNameWithoutExtension(path), entries);
        }

        return files;
    }

    private static IReadOnlyDictionary<string, string> English => Files.Value["en"];

    public static TheoryData<string> LanguageCodes() => new(ExpectedCodes);

    [Fact]
    public void StringFiles_contain_exactly_the_nine_languages_of_the_spec()
    {
        Assert.Equal(ExpectedCodes.Order(StringComparer.Ordinal), Files.Value.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void StringFiles_all_languages_have_every_english_key()
    {
        var missing = new List<string>();
        foreach (var (code, entries) in Files.Value)
        {
            missing.AddRange(English.Keys.Where(key => !entries.ContainsKey(key)).Select(key => $"{code}: {key}"));
        }

        Assert.True(missing.Count == 0, "Missing translations:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void StringFiles_have_no_key_that_english_lacks()
    {
        var stray = new List<string>();
        foreach (var (code, entries) in Files.Value)
        {
            stray.AddRange(entries.Keys.Where(key => !English.ContainsKey(key)).Select(key => $"{code}: {key}"));
        }

        Assert.True(stray.Count == 0, "Keys unknown to en.json (typos?):\n" + string.Join("\n", stray));
    }

    [Fact]
    public void StringFiles_have_no_blank_value()
    {
        var blank = new List<string>();
        foreach (var (code, entries) in Files.Value)
        {
            blank.AddRange(entries.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => $"{code}: {e.Key}"));
        }

        Assert.True(blank.Count == 0, "Blank values:\n" + string.Join("\n", blank));
    }

    [Fact]
    public void StringFiles_placeholders_match_english()
    {
        var placeholder = new Regex(@"\{\d+\}", RegexOptions.CultureInvariant);
        var mismatches = new List<string>();
        foreach (var (code, entries) in Files.Value)
        {
            foreach (var (key, english) in English)
            {
                var expected = placeholder.Matches(english).Select(m => m.Value).Order(StringComparer.Ordinal);
                var actual = placeholder.Matches(entries[key]).Select(m => m.Value).Order(StringComparer.Ordinal);
                if (!expected.SequenceEqual(actual))
                {
                    mismatches.Add($"{code}: {key}");
                }
            }
        }

        Assert.True(mismatches.Count == 0, "Placeholders differ from en.json:\n" + string.Join("\n", mismatches));
    }

    [Theory]
    [MemberData(nameof(LanguageCodes))]
    public void StringFiles_are_flat_json_objects_in_utf8_without_bom(string code)
    {
        var bytes = File.ReadAllBytes(Path.Combine(StringsDirectory.Value, code + ".json"));

        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "UTF-8 BOM found");

        // JsonDocument itself accepts bytes that are not valid UTF-8, so decode strictly first (throws on an
        // ANSI-saved file); the parse below is strict about comments and trailing commas.
        var text = StrictUtf8.GetString(bytes);
        Assert.DoesNotContain('�', text);

        using var document = JsonDocument.Parse(text);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.All(document.RootElement.EnumerateObject(), p => Assert.Equal(JsonValueKind.String, p.Value.ValueKind));
    }

    [Fact]
    public void StringFiles_have_no_duplicate_keys()
    {
        // JsonDocument keeps duplicates, unlike the dictionaries built by LoadAll, which would throw.
        foreach (var code in ExpectedCodes)
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(StringsDirectory.Value, code + ".json")));
            var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        }
    }

    [Fact]
    public void StringFiles_load_through_the_localizer_with_their_native_names()
    {
        var localizer = Localizer.LoadFromDirectory(StringsDirectory.Value);

        Assert.Equal(
            new (string, string)[]
            {
                ("en", "English"),
                ("de", "Deutsch"),
                ("es", "Español"),
                ("fr", "Français"),
                ("it", "Italiano"),
                ("nl", "Nederlands"),
                ("pl", "Polski"),
                ("pt-BR", "Português (Brasil)"),
                ("pt-PT", "Português (Portugal)"),
            },
            localizer.Available);
    }

    [Theory]
    [InlineData("en", "Edit", "Done")]
    [InlineData("fr", "Modifier", "Terminé")]
    [InlineData("es", "Editar", "Listo")]
    [InlineData("de", "Bearbeiten", "Fertig")]
    [InlineData("it", "Modifica", "Fine")]
    [InlineData("pt-BR", "Editar", "Concluído")]
    [InlineData("pt-PT", "Editar", "Concluído")]
    [InlineData("nl", "Bewerken", "Klaar")]
    [InlineData("pl", "Edytuj", "Gotowe")]
    public void StringFiles_customize_labels_match_spec(string code, string customize, string done)
    {
        var entries = Files.Value[code];

        Assert.Equal(customize, entries["Window.Customize"]);
        Assert.Equal(done, entries["Window.Done"]);
        // The tray menu entry carries the same word as the mode button.
        Assert.Equal(customize, entries["Tray.Customize"]);
    }

    [Fact]
    public void StringFiles_french_copy_fixed_by_the_spec()
    {
        var expected = new Dictionary<string, string>
        {
            ["Window.Customize"] = "Modifier",
            ["Window.Done"] = "Terminé",
            ["Board.AddColumn"] = "+ Colonne",
            ["Board.AddGroup"] = "+ Groupe",
            ["Tab.New"] = "Nouvel onglet",
            ["Group.New"] = "Nouveau groupe",
            ["AddMenu.Button"] = "Ajouter",
            ["AddMenu.CurrentFolder"] = "Dossier courant",
            ["AddMenu.CurrentUrl"] = "URL courante",
            ["AddMenu.Folder"] = "Dossier…",
            ["AddMenu.SpecialFolders"] = "Dossiers spéciaux",
            ["AddMenu.File"] = "Fichier…",
            ["AddMenu.Url"] = "URL…",
            ["AddMenu.UrlHint"] = "coller ou saisir",
            ["AddMenu.FromExplorer"] = "Appel depuis l'Explorateur",
            ["AddMenu.FromFileDialog"] = "Appel depuis une boîte de dialogue de fichiers",
            ["Item.BrowseHint"] = "sélecteur Windows",
            ["Item.CopyAddress"] = "Copier l'adresse",
            ["Item.FoldersOnly"] = "Seuls les dossiers peuvent être ouverts dans cette fenêtre.",
            ["UrlPrompt.Title"] = "Ajouter un site web",
            ["UrlPrompt.Address"] = "Adresse",
            ["UrlPrompt.Add"] = "Ajouter",
            ["UrlPrompt.Invalid"] = "Saisissez une adresse web, par exemple morningstar.fr ou https://www.morningstar.fr.",
            ["Default.SampleGroup"] = "Windows",
            ["Key.Shift"] = "Maj",
            ["Key.Win"] = "⊞ Win",
            ["Tray.Open"] = "Ouvrir",
            ["Tray.Customize"] = "Modifier",
            ["Tray.Quit"] = "Quitter",
        };

        var french = Files.Value["fr"];

        foreach (var (key, text) in expected)
        {
            Assert.Equal(text, french[key]);
        }
    }

    [Fact]
    public void StringFiles_french_names_of_themes_backgrounds_colors_and_special_folders()
    {
        var expected = new Dictionary<string, string>
        {
            ["Theme.fluent"] = "Fluent",
            ["Theme.cards"] = "Cartes",
            ["Theme.pastel"] = "Pastel",
            ["Theme.paper"] = "Papier",
            ["Theme.outline"] = "Contour",
            ["Theme.neon"] = "Néon",
            ["Theme.compact"] = "Compact",
            ["Background.white"] = "Blanc",
            ["Background.offWhite"] = "Blanc cassé",
            ["Background.cream"] = "Crème",
            ["Background.pearl"] = "Gris perle",
            ["Background.glacier"] = "Bleu glacier",
            ["Background.mint"] = "Vert d'eau",
            ["Background.blush"] = "Rose poudré",
            ["Background.black"] = "Noir",
            ["Background.charcoal"] = "Charbon",
            ["Background.anthracite"] = "Anthracite",
            ["Background.graphite"] = "Graphite",
            ["Background.slate"] = "Ardoise",
            ["Background.midnight"] = "Bleu nuit",
            ["Background.forest"] = "Vert forêt",
            ["Background.aubergine"] = "Aubergine",
            ["Color.blue"] = "Bleu",
            ["Color.cyan"] = "Cyan",
            ["Color.green"] = "Vert",
            ["Color.olive"] = "Olive",
            ["Color.amber"] = "Ambre",
            ["Color.orange"] = "Orange",
            ["Color.red"] = "Rouge",
            ["Color.pink"] = "Rose",
            ["Color.violet"] = "Violet",
            ["Color.slate"] = "Ardoise",
            ["SpecialFolder.Desktop"] = "Bureau",
            ["SpecialFolder.Documents"] = "Documents",
            ["SpecialFolder.Downloads"] = "Téléchargements",
            ["SpecialFolder.Pictures"] = "Images",
            ["SpecialFolder.Music"] = "Musique",
            ["SpecialFolder.Videos"] = "Vidéos",
            ["SpecialFolder.OneDrive"] = "OneDrive",
            ["SpecialFolder.UserProfile"] = "Dossier personnel",
            ["SpecialFolder.ThisPC"] = "Ce PC",
            ["SpecialFolder.RecycleBin"] = "Corbeille",
            ["SpecialFolder.ProgramFiles"] = "Program Files",
            ["SpecialFolder.RoamingAppData"] = "AppData (Roaming)",
            ["SpecialFolder.LocalAppData"] = "AppData (Local)",
            ["SpecialFolder.Temp"] = "Temp",
            ["SpecialFolder.Startup"] = "Démarrage",
            ["SpecialFolder.Windows"] = "Windows",
        };

        var french = Files.Value["fr"];

        foreach (var (key, text) in expected)
        {
            Assert.Equal(text, french[key]);
        }
    }

    [Fact]
    public void StringFiles_english_names_of_themes_and_colors()
    {
        string[] themes = ["fluent", "cards", "pastel", "paper", "outline", "neon", "compact"];
        string[] themeNames = ["Fluent", "Cards", "Pastel", "Paper", "Outline", "Neon", "Compact"];
        string[] colors = ["blue", "cyan", "green", "olive", "amber", "orange", "red", "pink", "violet", "slate"];
        string[] colorNames = ["Blue", "Cyan", "Green", "Olive", "Amber", "Orange", "Red", "Pink", "Violet", "Slate"];

        Assert.Equal(themeNames, themes.Select(key => English["Theme." + key]));
        Assert.Equal(colorNames, colors.Select(key => English["Color." + key]));
    }

    [Fact]
    public void StringFiles_have_the_background_keys_of_the_theme_catalog()
    {
        string[] backgrounds =
        [
            "white", "offWhite", "cream", "pearl", "glacier", "mint", "blush",
            "black", "charcoal", "anthracite", "graphite", "slate", "midnight", "forest", "aubergine",
        ];

        foreach (var key in backgrounds)
        {
            Assert.True(English.ContainsKey("Background." + key), "Background." + key);
        }
    }

    [Fact]
    public void StringFiles_cover_every_special_folder_of_the_catalog()
    {
        foreach (var info in SpecialFolderCatalog.All)
        {
            Assert.True(English.ContainsKey(info.NameKey), info.NameKey);
        }
    }

    [Fact]
    public void StringFiles_cover_every_theme_mode_and_group_radius()
    {
        foreach (var mode in Enum.GetNames<ThemeMode>())
        {
            Assert.True(English.ContainsKey("Appearance.Mode." + mode), "Appearance.Mode." + mode);
        }

        foreach (var radius in Enum.GetNames<GroupRadius>())
        {
            Assert.True(English.ContainsKey("Radius." + radius), "Radius." + radius);
        }
    }

    [Theory]
    [InlineData("SingleModifier")]
    [InlineData("NoModifier")]
    [InlineData("ReservedByWindows")]
    [InlineData("CtrlAltLikeAltGr")]
    [InlineData("TakenByOtherApp")]
    public void StringFiles_have_a_message_for_every_hotkey_issue(string issue)
    {
        Assert.True(English.ContainsKey("Hotkey.Issue." + issue));
    }

    [Fact]
    public void StringFiles_hotkey_texts_that_receive_the_shortcut_have_one_placeholder()
    {
        foreach (var entries in Files.Value.Values)
        {
            Assert.Contains("{0}", entries["Hotkey.Test"]);
            Assert.Contains("{0}", entries["Hotkey.Reset"]);
            Assert.Contains("{0}", entries["Tray.Ready"]);
            Assert.Contains("{0}", entries["Setup.OpenWith"]);
            Assert.DoesNotContain("{1}", entries["Hotkey.Test"]);
            Assert.DoesNotContain("{1}", entries["Hotkey.Reset"]);
            Assert.DoesNotContain("{1}", entries["Tray.Ready"]);
            Assert.DoesNotContain("{1}", entries["Setup.OpenWith"]);
            Assert.DoesNotContain("{0}", entries["Hotkey.ApplyToTest"]);
        }
    }

    [Fact]
    public void StringFiles_browser_footer_receives_the_browser_name()
    {
        foreach (var entries in Files.Value.Values)
        {
            Assert.Contains("{0}", entries["AddMenu.FromBrowser"]);
            Assert.DoesNotContain("{1}", entries["AddMenu.FromBrowser"]);
        }
    }

    [Fact]
    public void StringFiles_url_tip_names_only_the_recognised_browsers()
    {
        foreach (var entries in Files.Value.Values)
        {
            foreach (var browser in Enum.GetNames<BrowserKind>())
            {
                Assert.Contains(browser, entries["AddMenu.UrlTip"]);
            }
        }
    }

    [Theory]
    [InlineData("Setup.Installed")]
    [InlineData("Setup.Updated")]
    public void StringFiles_setup_texts_receive_the_version(string key)
    {
        foreach (var entries in Files.Value.Values)
        {
            Assert.Contains("{0}", entries[key]);
            Assert.DoesNotContain("{1}", entries[key]);
        }
    }

    [Fact]
    public void StringFiles_sample_group_is_called_windows_in_every_language()
    {
        Assert.All(Files.Value.Values, entries => Assert.Equal("Windows", entries["Default.SampleGroup"]));
    }

    [Fact]
    public void StringFiles_windows_key_is_the_same_in_every_language()
    {
        Assert.All(Files.Value.Values, entries => Assert.Equal("⊞ Win", entries["Key.Win"]));
    }
}
