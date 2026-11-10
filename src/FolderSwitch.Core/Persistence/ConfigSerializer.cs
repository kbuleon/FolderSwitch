// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Web;

namespace FolderSwitch.Core.Persistence;

/// <summary>
/// Converts an <see cref="AppConfig"/> to and from its JSON text: indented, camelCase properties,
/// enumerations as camelCase strings. Reading is strict about structure (see <see cref="Deserialize"/>)
/// and tolerant about content (colors, icons, items without a target, null lists and names). A separator is written
/// with its <c>id</c> and <c>kind</c> only; read back, whatever else it carries is dropped.
/// </summary>
public static class ConfigSerializer
{
    /// <summary>The newest schema version this build can read.</summary>
    /// <remarks>
    /// Version 2 adds the <c>url</c> item kind, version 3 the <c>separator</c> item kind; a version 1 or 2 file is read
    /// as is.
    /// </remarks>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Color given to a group whose stored color is missing or malformed (the palette's blue).</summary>
    public const string DefaultGroupColor = "#3b82f6";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // The file is hand-editable and never embedded in HTML: keep accents and punctuation readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { UseReadableIcons, WriteSeparatorsBare } },
    };

    /// <summary>The properties of an <see cref="Item"/> a separator has no use for, as written in the file.</summary>
    private static readonly HashSet<string> TargetProperties = ["name", "path", "special", "url"];

    /// <summary>A separator has no name and no target: only its <c>id</c> and <c>kind</c> are written.</summary>
    private static void WriteSeparatorsBare(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(Item))
        {
            return;
        }

        foreach (var property in typeInfo.Properties.Where(property => TargetProperties.Contains(property.Name)))
        {
            property.ShouldSerialize = static (item, _) => !((Item)item).IsSeparator;
        }
    }

    /// <summary>
    /// The encoder always escapes characters outside the Basic Multilingual Plane (<c>💼</c>), which
    /// is where most emojis live: the <c>icon</c> of a tab or a group gets a converter that writes it as is.
    /// </summary>
    private static void UseReadableIcons(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(Tab) && typeInfo.Type != typeof(Group))
        {
            return;
        }

        foreach (var property in typeInfo.Properties.Where(property => property.Name == "icon"))
        {
            property.CustomConverter = IconConverter.Instance;
        }
    }

    /// <summary>Reads a string; writes a plausible icon without escaping it (anything else is escaped as usual).</summary>
    private sealed class IconConverter : JsonConverter<string?>
    {
        public static readonly IconConverter Instance = new();

        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("An icon must be a string.");
            }

            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            // A plausible icon holds no quote, backslash or control character: it is safe to write verbatim.
            if (IconText.IsPlausible(value))
            {
                writer.WriteRawValue("\"" + value + "\"");
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }
    }

    public static string Serialize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return JsonSerializer.Serialize(config, Options);
    }

    /// <summary>Parses and validates a configuration.</summary>
    /// <exception cref="ConfigFormatException">
    /// The text is not valid JSON, <c>schemaVersion</c> is missing or newer than
    /// <see cref="CurrentSchemaVersion"/>, a value has the wrong type, or there is no tab.
    /// </exception>
    public static AppConfig Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        AppConfig config;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ConfigFormatException("The configuration must be a JSON object.");
            }

            CheckSchemaVersion(root);
            config = root.Deserialize<AppConfig>(Options)
                ?? throw new ConfigFormatException("The configuration is empty.");
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException("The configuration is not valid: " + ex.Message, ex);
        }

        Normalize(config);
        return config;
    }

    private static void CheckSchemaVersion(JsonElement root)
    {
        if (!root.TryGetProperty("schemaVersion", out var element))
        {
            throw new ConfigFormatException("The configuration has no schemaVersion.");
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var version))
        {
            throw new ConfigFormatException("The schemaVersion must be an integer.");
        }

        if (version > CurrentSchemaVersion)
        {
            throw new ConfigFormatException(
                $"The configuration uses schema version {version}, newer than the supported version {CurrentSchemaVersion}.");
        }
    }

    /// <summary>
    /// Repairs what a hand-edited or damaged file may leave behind so that the rest of the application
    /// never meets a null list, a null name, an invalid color or an item that points nowhere.
    /// </summary>
    private static void Normalize(AppConfig config)
    {
        // An older file is written back in the current format.
        config.SchemaVersion = CurrentSchemaVersion;

        config.Settings ??= new Settings();
        config.Settings.Hotkey ??= Hotkey.Default;

        config.Appearance ??= new Appearance();
        config.Appearance.Theme ??= new Appearance().Theme;
        config.Appearance.Backgrounds ??= new Dictionary<string, string>();
        foreach (var key in config.Appearance.Backgrounds.Where(pair => pair.Value is null).Select(pair => pair.Key).ToList())
        {
            config.Appearance.Backgrounds.Remove(key);
        }

        config.Tabs ??= new List<Tab>();
        config.Tabs.RemoveAll(tab => tab is null);
        if (config.Tabs.Count == 0)
        {
            throw new ConfigFormatException("The configuration has no tab.");
        }

        foreach (var tab in config.Tabs)
        {
            tab.Name ??= string.Empty;
            tab.Icon = IconText.IsPlausible(tab.Icon) ? tab.Icon : null;
            tab.Columns ??= new List<Column>();
            tab.Columns.RemoveAll(column => column is null);
            if (tab.Columns.Count == 0)
            {
                // A tab keeps at least one column (older versions let the last one be removed).
                tab.Columns.Add(new Column());
            }

            foreach (var column in tab.Columns)
            {
                column.Groups ??= new List<Group>();
                column.Groups.RemoveAll(group => group is null);

                foreach (var group in column.Groups)
                {
                    group.Name ??= string.Empty;
                    group.Icon = IconText.IsPlausible(group.Icon) ? group.Icon : null;
                    if (!IsHexColor(group.Color))
                    {
                        group.Color = DefaultGroupColor;
                    }

                    group.Items ??= new List<Item>();
                    group.Items.RemoveAll(item => item is null || !HasTarget(item));
                    foreach (var separator in group.Items.Where(item => item.IsSeparator))
                    {
                        ClearTarget(separator);
                    }
                }
            }
        }
    }

    /// <summary>Whether an item may stay: a separator always does, as it needs no target.</summary>
    private static bool HasTarget(Item item) =>
        item.Kind switch
        {
            ItemKind.Separator => true,
            ItemKind.Url => WebAddress.IsSiteUrl(item.Url),
            _ => !string.IsNullOrWhiteSpace(item.Path) || item.Special is not null,
        };

    /// <summary>A separator carries no name, path, special folder or address, whatever a hand-edited file gave it.</summary>
    private static void ClearTarget(Item separator)
    {
        separator.Name = null;
        separator.Path = null;
        separator.Special = null;
        separator.Url = null;
    }

    /// <summary>True for <c>#RRGGBB</c> (either case of the hex digits).</summary>
    private static bool IsHexColor(string? color)
    {
        if (color is not { Length: 7 } || color[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < color.Length; i++)
        {
            if (!Uri.IsHexDigit(color[i]))
            {
                return false;
            }
        }

        return true;
    }
}
