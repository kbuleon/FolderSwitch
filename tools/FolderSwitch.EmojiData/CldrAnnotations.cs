// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text.Json;

namespace FolderSwitch.EmojiData;

/// <summary>What CLDR says of one emoji in one language: its name (<c>tts</c>) and its search keywords (<c>default</c>).</summary>
internal sealed record Annotation(string Name, IReadOnlyList<string> Keywords);

/// <summary>
/// The CLDR annotations of one language, read from the <c>cldr-json</c> files of the language and of the languages it
/// inherits from (<c>pt-PT</c> only says what differs from <c>pt</c>). Keys are emojis without U+FE0F.
/// </summary>
internal sealed class CldrAnnotations
{
    private readonly Dictionary<string, Annotation> _entries = new(StringComparer.Ordinal);

    /// <summary>Adds the content of an <c>annotations.json</c> or <c>annotationsDerived/…/annotations.json</c> file; what it says replaces what was loaded before, entry by entry, field by field (name, keywords).</summary>
    public void Load(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var section = root.TryGetProperty("annotations", out var annotations) ? annotations : root.GetProperty("annotationsDerived");

        foreach (var entry in section.GetProperty("annotations").EnumerateObject())
        {
            var key = EmojiTestFile.BaseOf(entry.Name);
            var existing = _entries.GetValueOrDefault(key);

            // An overlay (pt-PT over pt) may carry only the keywords or only the name of an emoji: what it does not
            // say stays as the language it overlays said it.
            var keywords = entry.Value.TryGetProperty("default", out var list)
                ? list.EnumerateArray().Select(item => item.GetString()!).ToList()
                : existing?.Keywords ?? [];
            var name = entry.Value.TryGetProperty("tts", out var tts) && tts.GetArrayLength() > 0
                ? tts[0].GetString()!
                : existing?.Name ?? string.Empty;
            _entries[key] = new Annotation(name, keywords);
        }
    }

    public Annotation? Find(string emoji) => _entries.GetValueOrDefault(EmojiTestFile.BaseOf(emoji));
}
