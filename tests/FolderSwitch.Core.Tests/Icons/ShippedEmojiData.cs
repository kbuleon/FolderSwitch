// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Core.Tests.Icons;

/// <summary>The data the application ships, found in the source tree: the Emoji folder and the translation files.</summary>
internal static class ShippedEmojiData
{
    /// <summary>The 9 languages of the application.</summary>
    public static readonly string[] Codes = ["en", "de", "es", "fr", "it", "nl", "pl", "pt-BR", "pt-PT"];

    private static readonly Lazy<string> AppDirectory = new(LocateAppDirectory);

    private static readonly Lazy<EmojiLibrary> LoadedLibrary = new(() => EmojiLibrary.LoadFromDirectory(EmojiDirectory));

    /// <summary><c>src/FolderSwitch.App/Emoji</c>.</summary>
    public static string EmojiDirectory => Path.Combine(AppDirectory.Value, "Emoji");

    /// <summary><c>src/FolderSwitch.App/Strings</c>.</summary>
    public static string StringsDirectory => Path.Combine(AppDirectory.Value, "Strings");

    /// <summary>The shipped library, loaded once.</summary>
    public static EmojiLibrary Library => LoadedLibrary.Value;

    /// <summary>A finder over the shipped library with a font that draws every emoji.</summary>
    public static IconFinder Finder() => new(Library, new AllGlyphs());

    private static string LocateAppDirectory()
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

        return Path.Combine(directory.FullName, "src", "FolderSwitch.App");
    }

    private sealed class AllGlyphs : IEmojiGlyphs
    {
        public bool CanDraw(string emoji) => true;
    }
}
