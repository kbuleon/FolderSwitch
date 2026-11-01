using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Persistence;

/// <summary>
/// Converts an <see cref="AppConfig"/> to and from its JSON text: indented, camelCase properties,
/// enumerations as camelCase strings. Reading is strict about structure (see <see cref="Deserialize"/>)
/// and tolerant about content (colors, items without a target, null lists and names).
/// </summary>
public static class ConfigSerializer
{
    /// <summary>The newest schema version this build can read.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Color given to a group whose stored color is missing or malformed (the palette's blue).</summary>
    public const string DefaultGroupColor = "#3b82f6";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // The file is hand-editable and never embedded in HTML: keep accents and punctuation readable.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

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
                    if (!IsHexColor(group.Color))
                    {
                        group.Color = DefaultGroupColor;
                    }

                    group.Items ??= new List<Item>();
                    group.Items.RemoveAll(item => item is null || !HasTarget(item));
                }
            }
        }
    }

    private static bool HasTarget(Item item) => !string.IsNullOrWhiteSpace(item.Path) || item.Special is not null;

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
