// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Text.Json;

namespace FolderSwitch.DemoGif;

/// <summary>The words of one scene that shows the application: its heading and its text.</summary>
internal sealed record SceneCaption(string Title, string Body);

/// <summary>The first scene: the tagline (the application's own <c>About.Description</c>) and a short label.</summary>
internal sealed record IntroCaption(string Tagline, string Label);

/// <summary>The last scene: one closing line and the address of the repository.</summary>
internal sealed record OutroCaption(string Line, string Url);

/// <summary>
/// The captions of one language, read from <c>Captions/&lt;language&gt;.json</c> next to the executable. The names of
/// the application's own elements (buttons, menu entries, keys) are those of its <c>Strings/&lt;language&gt;.json</c>.
/// </summary>
internal sealed record Captions(
    IntroCaption Intro,
    SceneCaption Board,
    IReadOnlyList<string> Keys,
    SceneCaption Add,
    SceneCaption Edit,
    SceneCaption Icons,
    SceneCaption Context,
    string DialogTitle,
    SceneCaption Settings,
    OutroCaption Outro)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>The captions of <paramref name="language"/>; throws when the file is missing or incomplete.</summary>
    public static Captions Load(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Captions", language + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No captions for the language '{language}'.", path);
        }

        var captions = JsonSerializer.Deserialize<Captions>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"{path} is empty.");
        captions.Validate(path);
        return captions;
    }

    private void Validate(string path)
    {
        var texts = new[]
        {
            Intro?.Tagline, Intro?.Label, Board?.Title, Board?.Body, Add?.Title, Add?.Body, Edit?.Title, Edit?.Body,
            Icons?.Title, Icons?.Body, Context?.Title, Context?.Body, DialogTitle, Settings?.Title, Settings?.Body,
            Outro?.Line, Outro?.Url,
        };
        if (texts.Any(string.IsNullOrWhiteSpace) || Keys is null || Keys.Count == 0 || Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException($"{path} lacks a caption.");
        }
    }
}
