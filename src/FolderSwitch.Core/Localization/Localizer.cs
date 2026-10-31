using System.Globalization;
using System.Text.Json;

namespace FolderSwitch.Core.Localization;

/// <summary>
/// Looks up interface texts by key in the current language, falling back through
/// <see cref="Languages.FallbackChain"/> and finally returning the key itself. Languages are the
/// <c>&lt;code&gt;.json</c> files of a directory: flat JSON objects mapping keys to texts, with a mandatory
/// <c>"_meta.nativeName"</c> entry. Adding a language means dropping a file; no code changes.
/// Not thread-safe: meant to be used from the UI thread.
/// </summary>
public sealed class Localizer
{
    /// <summary>Mandatory entry of every language file: the language's own name, shown in language lists.</summary>
    public const string NativeNameKey = "_meta.nativeName";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly JsonDocumentOptions LenientJson = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Dictionary<string, Language> _languages;
    private Language? _current;
    private Language[] _chain = [];

    private Localizer(List<Language> languages)
    {
        _languages = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages)
        {
            _languages[language.Code] = language;
        }

        Available = languages
            .Select(language => (language.Code, language.NativeName))
            .ToArray()
            .AsReadOnly();

        var start = languages.Find(l => l.Code.Equals(Languages.DefaultCode, StringComparison.OrdinalIgnoreCase))
            ?? languages.FirstOrDefault();
        if (start is not null)
        {
            Apply(start);
        }
    }

    /// <summary>Raised after the language changed, once the new language is in place.</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// The languages that could be loaded: English first, then the others by code. Each has the code
    /// taken from its file name and the native name from <c>_meta.nativeName</c>.
    /// </summary>
    public IReadOnlyList<(string Code, string NativeName)> Available { get; }

    /// <summary>The code of the current language, as spelled by its file name. English when nothing was loaded.</summary>
    public string Code => _current?.Code ?? Languages.DefaultCode;

    /// <summary>
    /// The text for <paramref name="key"/> in the current language; when missing there, in the languages of its
    /// fallback chain; when missing everywhere, the key itself.
    /// </summary>
    public string this[string key]
    {
        get
        {
            ArgumentNullException.ThrowIfNull(key);

            foreach (var language in _chain)
            {
                if (language.Strings.TryGetValue(key, out var text))
                {
                    return text;
                }
            }

            return key;
        }
    }

    /// <summary>
    /// Loads every valid <c>*.json</c> language file of <paramref name="dir"/>. Starts in English when available.
    /// Never throws for a missing directory or a bad file: an unreadable, malformed, not valid UTF-8 (saved as ANSI)
    /// or <c>_meta.nativeName</c>-less file is skipped, and with no language at all every text is its own key.
    /// </summary>
    public static Localizer LoadFromDirectory(string dir)
    {
        ArgumentNullException.ThrowIfNull(dir);

        var languages = new List<Language>();
        if (Directory.Exists(dir))
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = Directory.EnumerateFiles(dir, "*.json")
                .Where(path => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                if (TryLoad(path) is { } language && seen.Add(language.Code))
                {
                    languages.Add(language);
                }
            }
        }

        languages.Sort(CompareForListing);
        return new Localizer(languages);
    }

    /// <summary>
    /// Switches the language and raises <see cref="LanguageChanged"/> (not when it is already the current one).
    /// </summary>
    /// <exception cref="ArgumentException">No language has this code; nothing changes.</exception>
    public void SetLanguage(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        if (!_languages.TryGetValue(code, out var language))
        {
            throw new ArgumentException($"Unknown language '{code}'.", nameof(code));
        }

        if (ReferenceEquals(language, _current))
        {
            return;
        }

        Apply(language);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// <see cref="string.Format(IFormatProvider, string, object[])"/> applied to the text of
    /// <paramref name="key"/>, using the culture of the current language. A translation whose placeholders are
    /// wrong does not throw: its text is returned unformatted.
    /// </summary>
    public string Format(string key, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var text = this[key];
        try
        {
            return string.Format(_current?.Culture ?? CultureInfo.InvariantCulture, text, args);
        }
        catch (FormatException)
        {
            return text;
        }
    }

    private static int CompareForListing(Language left, Language right)
    {
        var leftIsDefault = left.Code.Equals(Languages.DefaultCode, StringComparison.OrdinalIgnoreCase);
        var rightIsDefault = right.Code.Equals(Languages.DefaultCode, StringComparison.OrdinalIgnoreCase);
        if (leftIsDefault != rightIsDefault)
        {
            return leftIsDefault ? -1 : 1;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left.Code, right.Code);
    }

    private static Language? TryLoad(string path)
    {
        var code = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            // A UTF-8 byte-order mark is common in files saved by hand; the JSON parser does not accept it.
            var bytes = File.ReadAllBytes(path).AsMemory();
            if (bytes.Span.StartsWith(Utf8Bom))
            {
                bytes = bytes[Utf8Bom.Length..];
            }

            using var document = JsonDocument.Parse(bytes, LenientJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String
                    && property.Value.GetString() is { } value
                    && !string.IsNullOrWhiteSpace(value))
                {
                    strings[property.Name] = value;
                }
            }

            if (!strings.TryGetValue(NativeNameKey, out var nativeName))
            {
                return null;
            }

            return new Language(code, nativeName, strings, CultureFor(code));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or IOException or UnauthorizedAccessException)
        {
            // A language file dropped by hand must never stop the application from starting.
            // InvalidOperationException: the parser accepts bytes that are not valid UTF-8 (a file saved as
            // ANSI, say) and only fails when a name or a value is read as text.
            return null;
        }
    }

    private static CultureInfo CultureFor(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private void Apply(Language language)
    {
        _current = language;
        _chain = Languages.FallbackChain(language.Code)
            .Select(code => _languages.GetValueOrDefault(code))
            .OfType<Language>()
            .ToArray();
    }

    private sealed record Language(
        string Code,
        string NativeName,
        IReadOnlyDictionary<string, string> Strings,
        CultureInfo Culture);
}
