// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using System.Text.Json;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Tests.Persistence;

namespace FolderSwitch.Core.Tests.Localization;

public class LocalizerTests
{
    private static void WriteLanguage(
        string directory,
        string code,
        string nativeName,
        params (string Key, string Value)[] entries)
    {
        var content = new Dictionary<string, string> { ["_meta.nativeName"] = nativeName };
        foreach (var (key, value) in entries)
        {
            content[key] = value;
        }

        File.WriteAllText(
            Path.Combine(directory, code + ".json"),
            JsonSerializer.Serialize(content),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static Localizer LoadWithPortuguese(TempDirectory dir)
    {
        WriteLanguage(dir.Root, "en", "English", ("Only.English", "English text"), ("Shared", "Shared EN"));
        WriteLanguage(dir.Root, "fr", "Français", ("Shared", "Partagé FR"));
        WriteLanguage(dir.Root, "pt-BR", "Português (Brasil)", ("Shared", "Compartilhado BR"), ("Only.Brazil", "Texto BR"));
        WriteLanguage(dir.Root, "pt-PT", "Português (Portugal)", ("Shared", "Partilhado PT"));
        return Localizer.LoadFromDirectory(dir.Root);
    }

    [Fact]
    public void Available_lists_the_files_present_with_english_first_then_by_code()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "pt-PT", "Português (Portugal)");
        WriteLanguage(dir.Root, "fr", "Français");
        WriteLanguage(dir.Root, "pl", "Polski");
        WriteLanguage(dir.Root, "en", "English");
        WriteLanguage(dir.Root, "de", "Deutsch");
        WriteLanguage(dir.Root, "pt-BR", "Português (Brasil)");

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal(
            new (string, string)[]
            {
                ("en", "English"),
                ("de", "Deutsch"),
                ("fr", "Français"),
                ("pl", "Polski"),
                ("pt-BR", "Português (Brasil)"),
                ("pt-PT", "Português (Portugal)"),
            },
            localizer.Available);
    }

    [Fact]
    public void Starts_in_english_when_available()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "fr", "Français", ("Hello", "Bonjour"));
        WriteLanguage(dir.Root, "en", "English", ("Hello", "Hello"));

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal("en", localizer.Code);
        Assert.Equal("Hello", localizer["Hello"]);
    }

    [Fact]
    public void Starts_in_the_first_language_when_english_is_absent()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "fr", "Français", ("Hello", "Bonjour"));
        WriteLanguage(dir.Root, "de", "Deutsch", ("Hello", "Hallo"));

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal("de", localizer.Code);
        Assert.Equal("Hallo", localizer["Hello"]);
    }

    [Fact]
    public void Indexer_returns_the_text_of_the_current_language()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);

        localizer.SetLanguage("fr");

        Assert.Equal("fr", localizer.Code);
        Assert.Equal("Partagé FR", localizer["Shared"]);
    }

    [Fact]
    public void Missing_key_falls_back_through_chain()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);
        localizer.SetLanguage("pt-PT");

        // Absent from pt-PT, present in pt-BR: the neighbouring variant answers.
        Assert.Equal("Texto BR", localizer["Only.Brazil"]);
        // Absent from both Portuguese variants, present in English.
        Assert.Equal("English text", localizer["Only.English"]);
        // Present in pt-PT itself: no fallback.
        Assert.Equal("Partilhado PT", localizer["Shared"]);
        // Absent everywhere: the key itself.
        Assert.Equal("Nowhere.To.Be.Found", localizer["Nowhere.To.Be.Found"]);
    }

    [Fact]
    public void Missing_key_in_brazilian_portuguese_falls_back_to_portugal_then_english()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Only.English", "English text"));
        WriteLanguage(dir.Root, "pt-BR", "Português (Brasil)");
        WriteLanguage(dir.Root, "pt-PT", "Português (Portugal)", ("Only.Portugal", "Texto PT"));
        var localizer = Localizer.LoadFromDirectory(dir.Root);
        localizer.SetLanguage("pt-BR");

        Assert.Equal("Texto PT", localizer["Only.Portugal"]);
        Assert.Equal("English text", localizer["Only.English"]);
    }

    [Fact]
    public void Missing_key_in_a_non_portuguese_language_falls_back_to_english_only()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);
        localizer.SetLanguage("fr");

        Assert.Equal("English text", localizer["Only.English"]);
        // pt-BR is not in the chain of fr.
        Assert.Equal("Only.Brazil", localizer["Only.Brazil"]);
    }

    [Fact]
    public void Blank_translations_count_as_missing()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Label", "Label EN"), ("Other", "Other EN"));
        WriteLanguage(dir.Root, "fr", "Français", ("Label", ""), ("Other", "   "));
        var localizer = Localizer.LoadFromDirectory(dir.Root);
        localizer.SetLanguage("fr");

        Assert.Equal("Label EN", localizer["Label"]);
        Assert.Equal("Other EN", localizer["Other"]);
    }

    [Fact]
    public void Indexer_rejects_a_null_key()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);

        Assert.Throws<ArgumentNullException>(() => localizer[null!]);
    }

    [Fact]
    public void SetLanguage_raises_LanguageChanged()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);
        var raised = new List<(object? Sender, string CodeSeen, string TextSeen)>();
        localizer.LanguageChanged += (sender, _) => raised.Add((sender, localizer.Code, localizer["Shared"]));

        localizer.SetLanguage("fr");

        var notification = Assert.Single(raised);
        Assert.Same(localizer, notification.Sender);
        // The new language is already in place when handlers run.
        Assert.Equal("fr", notification.CodeSeen);
        Assert.Equal("Partagé FR", notification.TextSeen);
    }

    [Fact]
    public void SetLanguage_to_the_current_language_does_not_raise()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);
        var count = 0;
        localizer.LanguageChanged += (_, _) => count++;

        localizer.SetLanguage("en");
        Assert.Equal(0, count);

        localizer.SetLanguage("fr");
        localizer.SetLanguage("fr");
        localizer.SetLanguage("FR");
        Assert.Equal(1, count);
    }

    [Fact]
    public void SetLanguage_ignores_case_and_exposes_the_code_as_loaded()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);

        localizer.SetLanguage("PT-br");

        Assert.Equal("pt-BR", localizer.Code);
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("pt")]
    public void SetLanguage_throws_for_an_unknown_code_and_changes_nothing(string code)
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);
        localizer.SetLanguage("fr");
        var raised = false;
        localizer.LanguageChanged += (_, _) => raised = true;

        Assert.Throws<ArgumentException>(() => localizer.SetLanguage(code));

        Assert.Equal("fr", localizer.Code);
        Assert.False(raised);
    }

    [Fact]
    public void SetLanguage_throws_for_a_null_code()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);

        Assert.Throws<ArgumentNullException>(() => localizer.SetLanguage(null!));
    }

    [Fact]
    public void Format_inserts_the_arguments()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Hotkey.Test", "Press {0} to try it ({1})"));
        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal("Press Ctrl + Win to try it (2)", localizer.Format("Hotkey.Test", "Ctrl + Win", 2));
    }

    [Fact]
    public void Format_uses_the_text_of_the_current_language_and_its_fallbacks()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Greeting", "Hello {0}"), ("Only.English", "Only {0}"));
        WriteLanguage(dir.Root, "fr", "Français", ("Greeting", "Bonjour {0}"));
        var localizer = Localizer.LoadFromDirectory(dir.Root);
        localizer.SetLanguage("fr");

        Assert.Equal("Bonjour Karl", localizer.Format("Greeting", "Karl"));
        Assert.Equal("Only Karl", localizer.Format("Only.English", "Karl"));
    }

    [Fact]
    public void Format_uses_the_culture_of_the_current_language()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Number", "{0}"));
        WriteLanguage(dir.Root, "fr", "Français", ("Number", "{0}"));
        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal("1.5", localizer.Format("Number", 1.5));

        localizer.SetLanguage("fr");
        Assert.Equal("1,5", localizer.Format("Number", 1.5));
    }

    [Fact]
    public void Format_with_a_language_that_has_no_matching_culture_does_not_throw()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Greeting", "Hello {0}"));
        WriteLanguage(dir.Root, "not-a-real-culture-code", "Made up", ("Greeting", "Hi {0}"));
        var localizer = Localizer.LoadFromDirectory(dir.Root);

        localizer.SetLanguage("not-a-real-culture-code");

        Assert.Equal("Hi there", localizer.Format("Greeting", "there"));
    }

    [Fact]
    public void Format_of_a_missing_key_returns_the_key()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);

        Assert.Equal("Nowhere.To.Be.Found", localizer.Format("Nowhere.To.Be.Found", "unused"));
    }

    [Fact]
    public void Format_with_a_broken_translation_returns_the_unformatted_text_instead_of_throwing()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Broken", "Value {0"), ("TooMany", "Needs {0} and {1}"));
        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal("Value {0", localizer.Format("Broken", "x"));
        Assert.Equal("Needs {0} and {1}", localizer.Format("TooMany", "x"));
    }

    [Fact]
    public void Format_without_arguments_returns_the_text()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Plain", "Just text"));
        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal("Just text", localizer.Format("Plain"));
    }

    [Fact]
    public void Adding_a_language_file_needs_no_code_change()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Hello", "Hello"), ("Bye", "Bye"));
        WriteLanguage(dir.Root, "xx", "Xxish", ("Hello", "Xello"));

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Contains(("xx", "Xxish"), localizer.Available);
        localizer.SetLanguage("xx");
        Assert.Equal("Xello", localizer["Hello"]);
        Assert.Equal("Bye", localizer["Bye"]);
    }

    [Fact]
    public void Files_that_are_not_valid_language_files_are_ignored()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Hello", "Hello"));
        File.WriteAllText(Path.Combine(dir.Root, "broken.json"), "{ not json");
        File.WriteAllText(Path.Combine(dir.Root, "truncated.json"), "{\"_meta.nativeName\": \"Trunc");
        File.WriteAllText(Path.Combine(dir.Root, "array.json"), "[\"_meta.nativeName\"]");
        File.WriteAllText(Path.Combine(dir.Root, "nometa.json"), "{\"Hello\": \"Hi\"}");
        File.WriteAllText(Path.Combine(dir.Root, "blankmeta.json"), "{\"_meta.nativeName\": \"  \"}");
        File.WriteAllText(Path.Combine(dir.Root, "numbermeta.json"), "{\"_meta.nativeName\": 3}");
        File.WriteAllText(Path.Combine(dir.Root, "empty.json"), "");
        File.WriteAllText(Path.Combine(dir.Root, "notes.txt"), "{\"_meta.nativeName\": \"Text\"}");
        File.WriteAllText(Path.Combine(dir.Root, "readme.md"), "# not a language");
        Directory.CreateDirectory(Path.Combine(dir.Root, "sub.json"));

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal(new (string, string)[] { ("en", "English") }, localizer.Available);
    }

    [Theory]
    [InlineData("{\"_meta.nativeName\": \"Français\", \"Hello\": \"Bonjour à tous\"}")]
    [InlineData("{\"Hello\": \"Bonjour\", \"_meta.nativeName\": \"Français\", \"Crème\": \"x\"}")]
    public void A_file_saved_as_ansi_instead_of_utf8_is_ignored_without_throwing(string content)
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Hello", "Hello"));
        // Latin-1 bytes: 'ç', 'à' and 'è' are single bytes >= 0x80, which are invalid UTF-8.
        File.WriteAllBytes(Path.Combine(dir.Root, "fr.json"), Encoding.Latin1.GetBytes(content));

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal(new (string, string)[] { ("en", "English") }, localizer.Available);
        Assert.Equal("en", localizer.Code);
    }

    [Fact]
    public void Entries_with_a_non_string_value_are_ignored_but_the_file_loads()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Count", "Count EN"));
        File.WriteAllText(
            Path.Combine(dir.Root, "fr.json"),
            "{\"_meta.nativeName\": \"Français\", \"Count\": 3, \"Nothing\": null, \"Nested\": {\"a\": \"b\"}, \"Ok\": \"Oui\"}");

        var localizer = Localizer.LoadFromDirectory(dir.Root);
        localizer.SetLanguage("fr");

        Assert.Equal("Count EN", localizer["Count"]);
        Assert.Equal("Oui", localizer["Ok"]);
    }

    [Fact]
    public void Language_files_may_carry_a_utf8_bom_comments_and_trailing_commas()
    {
        using var dir = new TempDirectory();
        var json = "{\n  // translated by hand\n  \"_meta.nativeName\": \"Français\",\n  \"Hello\": \"Bonjour à tous\",\n}";
        File.WriteAllText(Path.Combine(dir.Root, "fr.json"), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var localizer = Localizer.LoadFromDirectory(dir.Root);
        localizer.SetLanguage("fr");

        Assert.Equal("Bonjour à tous", localizer["Hello"]);
    }

    [Fact]
    public void Non_ascii_text_round_trips()
    {
        using var dir = new TempDirectory();
        WriteLanguage(dir.Root, "en", "English", ("Symbols", "⊞ Win · … « é » ✓ ⚙"));

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Equal("⊞ Win · … « é » ✓ ⚙", localizer["Symbols"]);
    }

    [Fact]
    public void Missing_directory_gives_an_empty_localizer_that_returns_keys()
    {
        using var dir = new TempDirectory();
        var missing = Path.Combine(dir.Root, "does-not-exist");

        var localizer = Localizer.LoadFromDirectory(missing);

        Assert.Empty(localizer.Available);
        Assert.Equal("en", localizer.Code);
        Assert.Equal("Window.Customize", localizer["Window.Customize"]);
        Assert.Throws<ArgumentException>(() => localizer.SetLanguage("en"));
    }

    [Fact]
    public void Empty_directory_gives_an_empty_localizer_that_returns_keys()
    {
        using var dir = new TempDirectory();

        var localizer = Localizer.LoadFromDirectory(dir.Root);

        Assert.Empty(localizer.Available);
        Assert.Equal("Window.Done", localizer["Window.Done"]);
        Assert.Equal("Window.Done", localizer.Format("Window.Done"));
    }

    [Fact]
    public void LoadFromDirectory_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => Localizer.LoadFromDirectory(null!));
    }

    [Fact]
    public void Available_cannot_be_modified_through_a_cast()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);

        if (localizer.Available is IList<(string Code, string NativeName)> list)
        {
            Assert.Throws<NotSupportedException>(() => list[0] = ("xx", "Xxish"));
        }

        Assert.Equal("en", localizer.Available[0].Code);
    }

    [Fact]
    public void Available_codes_feed_language_selection()
    {
        using var dir = new TempDirectory();
        var localizer = LoadWithPortuguese(dir);

        var initial = Languages.SelectInitial("pt-AO", localizer.Available.Select(l => l.Code).ToArray());

        Assert.Equal("pt-PT", initial);
        localizer.SetLanguage(initial);
        Assert.Equal("Partilhado PT", localizer["Shared"]);
    }
}
