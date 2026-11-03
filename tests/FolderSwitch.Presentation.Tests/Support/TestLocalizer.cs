// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text;
using System.Text.Json;
using FolderSwitch.Core.Localization;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>A real <see cref="Localizer"/> loaded from small English and French files written to a temp folder.</summary>
internal static class TestLocalizer
{
    public static Localizer Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FolderSwitch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Write(directory, "en", new Dictionary<string, string>
            {
                ["_meta.nativeName"] = "English",
                ["Window.Customize"] = "Customize",
                ["Window.Done"] = "Done",
                ["AddMenu.FromExplorer"] = "Called from File Explorer",
                ["AddMenu.FromFileDialog"] = "Called from a file dialog",
                ["AddMenu.FromBrowser"] = "Called from the browser: {0}",
                ["Tab.New"] = "New tab",
                ["Group.New"] = "New group",
                ["SpecialFolder.Desktop"] = "Desktop",
                ["SpecialFolder.Documents"] = "Documents",
                ["SpecialFolder.ThisPC"] = "This PC",
                ["Error.ActionFailed"] = "The action could not be completed.",
                ["Key.Ctrl"] = "Ctrl",
                ["Key.Alt"] = "Alt",
                ["Key.Shift"] = "Shift",
                ["Key.Win"] = "⊞ Win",
                ["Key.Space"] = "Space",
                ["Hotkey.Reset"] = "Reset to {0}",
                ["Hotkey.Test"] = "Try the shortcut: press {0}",
                ["Hotkey.ApplyToTest"] = "Apply this shortcut first, then try it here.",
                ["Hotkey.Issue.SingleModifier"] = "A single modifier key is not enough.",
                ["Hotkey.Issue.NoModifier"] = "Add at least one modifier key.",
                ["Hotkey.Issue.ReservedByWindows"] = "This shortcut is reserved by Windows.",
                ["Hotkey.Issue.CtrlAltLikeAltGr"] = "Ctrl + Alt is the same as AltGr.",
                ["Hotkey.Issue.TakenByOtherApp"] = "This shortcut is already used by another application.",
                ["Default.SampleTab"] = "Example",
                ["Default.SampleGroup"] = "Windows",
                ["Default.EmptyTab"] = "My folders",
                ["Icon.Category.SmileysEmotion"] = "Smileys & emotion",
                ["Icon.Category.PeopleBody"] = "People",
                ["Icon.Category.AnimalsNature"] = "Animals & nature",
                ["Icon.Category.FoodDrink"] = "Food & drink",
                ["Icon.Category.TravelPlaces"] = "Travel & places",
                ["Icon.Category.Activities"] = "Activities",
                ["Icon.Category.Objects"] = "Objects",
                ["Icon.Category.Symbols"] = "Symbols",
            });
            Write(directory, "fr", new Dictionary<string, string>
            {
                ["_meta.nativeName"] = "Français",
                ["Window.Customize"] = "Configurer",
                ["Window.Done"] = "Terminer",
                ["AddMenu.FromExplorer"] = "Appel depuis l'Explorateur",
                ["Tab.New"] = "Nouvel onglet",
                ["Group.New"] = "Nouveau groupe",
                ["SpecialFolder.Desktop"] = "Bureau",
                ["Error.ActionFailed"] = "L'action n'a pas pu être effectuée.",
                ["Key.Shift"] = "Maj",
                ["Key.Space"] = "Espace",
                ["Hotkey.Reset"] = "Revenir à {0}",
                ["Hotkey.Test"] = "Essayez le raccourci : appuyez sur {0}",
                ["Hotkey.Issue.ReservedByWindows"] = "Ce raccourci est réservé par Windows.",
                ["Default.SampleTab"] = "Exemple",
                ["Default.EmptyTab"] = "Mes dossiers",
                ["Icon.Category.SmileysEmotion"] = "Smileys et émotions",
                ["Icon.Category.AnimalsNature"] = "Animaux et nature",
                ["Icon.Category.TravelPlaces"] = "Voyages et lieux",
                ["Icon.Category.Objects"] = "Objets",
            });

            // The localizer keeps every text in memory: the files are no longer needed once it is loaded.
            return Localizer.LoadFromDirectory(directory);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: a leftover temp folder must not fail a test.
            }
        }
    }

    private static void Write(string directory, string code, Dictionary<string, string> entries)
    {
        File.WriteAllText(
            Path.Combine(directory, code + ".json"),
            JsonSerializer.Serialize(entries),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
