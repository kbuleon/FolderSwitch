// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FolderSwitch.EmojiData;

/// <summary>
/// <c>dotnet run --project tools/FolderSwitch.EmojiData -- &lt;working folder&gt; [--sheet]</c>: downloads the Unicode emoji
/// list and the CLDR annotations into the working folder (never into the repository), and writes the emoji data the
/// application ships: <c>catalog.json</c> and <c>keywords/&lt;code&gt;.json</c> in <c>src/FolderSwitch.App/Emoji</c>.
/// With <c>--sheet</c>, also writes one contact sheet per category (PNG) into the working folder. Exit code 0 when the
/// files are written, 1 for a wrong command line, 2 for any failure.
/// </summary>
internal static partial class Program
{
    /// <summary>The Unicode Emoji version (<c>emoji-test.txt</c>): the latest published when the data was last generated.</summary>
    private const string UnicodeVersion = "18.0";

    /// <summary>The <c>unicode-org/cldr-json</c> release tag (never a branch): the latest published when the data was last generated.</summary>
    private const string CldrRelease = "49.0.0";

    /// <summary>Shipped language codes with the CLDR folders each one reads, most specific first (CLDR has no <c>pt-BR</c>: it is <c>pt</c>; <c>pt-PT</c> says only what differs from <c>pt</c>).</summary>
    private static readonly (string Code, string[] CldrLocales)[] Languages =
    [
        ("en", ["en"]),
        ("de", ["de"]),
        ("es", ["es"]),
        ("fr", ["fr"]),
        ("it", ["it"]),
        ("nl", ["nl"]),
        ("pl", ["pl"]),
        ("pt-BR", ["pt"]),
        ("pt-PT", ["pt-PT", "pt"]),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Same as the configuration file: keep accents and emojis readable, the files are never embedded in HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // WPF draws on an STA thread.
    [STAThread]
    private static int Main(string[] args)
    {
        var sheet = args.Contains("--sheet", StringComparer.Ordinal);
        var positional = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (positional.Count != 1 || args.Length - positional.Count > (sheet ? 1 : 0))
        {
            Console.Error.WriteLine("Usage: FolderSwitch.EmojiData <working folder> [--sheet]   (the working folder must be outside the repository)");
            return 1;
        }

        try
        {
            var repository = FindRepository();
            var workingFolder = Path.GetFullPath(positional[0]);
            if (IsInside(workingFolder, repository))
            {
                Console.Error.WriteLine("The working folder must be outside the repository: " + workingFolder);
                return 1;
            }

            Directory.CreateDirectory(workingFolder);
            Run(repository, workingFolder, sheet).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    private static async Task Run(string repository, string workingFolder, bool sheet)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FolderSwitch.EmojiData");

        // 1. The sources.
        var emojiTestText = await Fetch(http, workingFolder, $"emoji-test-{UnicodeVersion}.txt", $"https://unicode.org/Public/{UnicodeVersion}.0/emoji/emoji-test.txt");
        var declared = EmojiTestFile.ReadVersion(emojiTestText);
        if (declared != UnicodeVersion)
        {
            throw new InvalidDataException($"emoji-test.txt declares version {declared}, expected {UnicodeVersion}.");
        }

        // 2. The rules of the catalog, then the hand list.
        var excluded = ReadExcluded(Path.Combine(repository, "tools", "FolderSwitch.EmojiData", "excluded.txt"));
        var candidates = new List<(string Category, string Emoji, string Name)>();
        foreach (var entry in EmojiTestFile.Parse(emojiTestText))
        {
            if (EmojiTestFile.CategoryOf(entry) is { } category)
            {
                candidates.Add((category, entry.Emoji, entry.Name));
            }
        }

        var byHand = candidates.Where(candidate => excluded.Contains(EmojiTestFile.BaseOf(candidate.Emoji))).ToList();
        var unknownInHandList = excluded.Where(hand => candidates.All(candidate => EmojiTestFile.BaseOf(candidate.Emoji) != hand)).ToList();
        var ruled = candidates.Count;
        candidates = candidates.Except(byHand).ToList();

        // 3. What this machine's Segoe UI Emoji cannot draw.
        var glyphs = EmojiFont.GlyphMap;
        var undrawable = candidates
            .Where(candidate => !EmojiTestFile.BaseOf(candidate.Emoji).EnumerateRunes().All(rune => glyphs.ContainsKey(rune.Value)) || !EmojiFont.HasOutline(candidate.Emoji))
            .ToList();
        var catalog = candidates.Except(undrawable).ToList();

        Console.WriteLine($"Unicode Emoji {UnicodeVersion}, CLDR {CldrRelease}.");
        Console.WriteLine($"The catalog rules kept {ruled}; the hand list removed {byHand.Count}; the font removed {undrawable.Count}; catalog: {catalog.Count}.");
        foreach (var (category, _) in EmojiTestFile.Categories)
        {
            Console.WriteLine($"  {category}: {catalog.Count(entry => entry.Category == category)}");
        }

        foreach (var hand in unknownInHandList)
        {
            Console.WriteLine($"  excluded.txt: {hand} is not an emoji the rules keep (obsolete line?).");
        }

        foreach (var entry in undrawable)
        {
            Console.WriteLine($"  not drawn by the font: {entry.Emoji} {entry.Name}");
        }

        // 4. The files.
        var emojiFolder = Path.Combine(repository, "src", "FolderSwitch.App", "Emoji");
        var keywordsFolder = Path.Combine(emojiFolder, "keywords");
        Directory.CreateDirectory(keywordsFolder);

        WriteJson(
            Path.Combine(emojiFolder, "catalog.json"),
            new CatalogFile(UnicodeVersion, catalog.Select(entry => new CatalogEntry(entry.Emoji, entry.Category)).ToList()));

        var cldrMajor = CldrRelease.Split('.')[0];
        foreach (var (code, locales) in Languages)
        {
            var annotations = new CldrAnnotations();
            foreach (var locale in locales.Reverse())
            {
                var derived = await Fetch(http, workingFolder, $"cldr-{CldrRelease}/annotationsDerived-{locale}.json", CldrUrl("cldr-annotations-derived-full/annotationsDerived", locale));
                var main = await Fetch(http, workingFolder, $"cldr-{CldrRelease}/annotations-{locale}.json", CldrUrl("cldr-annotations-full/annotations", locale));
                annotations.Load(derived);
                annotations.Load(main);
            }

            var emojis = new Dictionary<string, KeywordEntry>(StringComparer.Ordinal);
            foreach (var entry in catalog)
            {
                if (annotations.Find(entry.Emoji) is { } annotation)
                {
                    emojis[entry.Emoji] = new KeywordEntry(annotation.Name, annotation.Keywords);
                }
            }

            WriteJson(Path.Combine(keywordsFolder, code + ".json"), new KeywordFile(cldrMajor, emojis));
            Console.WriteLine($"  keywords/{code}.json: {emojis.Count} of {catalog.Count} emojis" + (emojis.Count < catalog.Count ? " (the others have no CLDR entry)" : string.Empty));
        }

        // 5. The contact sheets.
        if (sheet)
        {
            var files = ContactSheet.Write(workingFolder, catalog.Select(entry => (entry.Category, entry.Emoji)).ToList());
            Console.WriteLine($"{files.Count} contact sheets in {workingFolder}.");
        }
    }

    private static string CldrUrl(string package, string locale) =>
        $"https://raw.githubusercontent.com/unicode-org/cldr-json/{CldrRelease}/cldr-json/{package}/{locale}/annotations.json";

    /// <summary>The text of a file of the working folder, downloaded first when it is not there yet. Downloaded files are data: they are only parsed.</summary>
    private static async Task<string> Fetch(HttpClient http, string workingFolder, string name, string url)
    {
        var path = Path.Combine(workingFolder, name);
        if (!File.Exists(path))
        {
            Console.WriteLine("Downloading " + url);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, await http.GetByteArrayAsync(url));
        }

        return await File.ReadAllTextAsync(path, Encoding.UTF8);
    }

    /// <summary>The emojis of <c>excluded.txt</c> (one per line, comment after <c>#</c>), without U+FE0F.</summary>
    private static HashSet<string> ReadExcluded(string path)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
        {
            var emoji = (line.Contains('#') ? line[..line.IndexOf('#')] : line).Trim();
            if (emoji.Length > 0)
            {
                excluded.Add(EmojiTestFile.BaseOf(emoji));
            }
        }

        return excluded;
    }

    private static void WriteJson<T>(string path, T value)
    {
        // The encoder writes every character outside the Basic Multilingual Plane (the emojis) as a pair of \uXXXX
        // escapes: put them back as the characters they stand for, so that the files read as the emojis they hold.
        var json = SurrogatePairEscape().Replace(
            JsonSerializer.Serialize(value, JsonOptions),
            match => string.Concat((char)Convert.ToInt32(match.Groups[1].Value, 16), (char)Convert.ToInt32(match.Groups[2].Value, 16)));

        // UTF-8 without a byte order mark, indented, with a final line break.
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // A high surrogate escape followed by a low surrogate escape, the backslash not itself escaped.
    [GeneratedRegex(@"(?<!\\)\\u([dD][89abAB][0-9a-fA-F]{2})\\u([dD][c-fC-F][0-9a-fA-F]{2})")]
    private static partial Regex SurrogatePairEscape();

    private static string FindRepository()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FolderSwitch.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("FolderSwitch.slnx was not found above " + AppContext.BaseDirectory);
    }

    private static bool IsInside(string path, string folder) =>
        Path.GetRelativePath(folder, path) is var relative && !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);

    private sealed record CatalogFile(string UnicodeVersion, List<CatalogEntry> Emoji);

    private sealed record CatalogEntry(string E, string C);

    private sealed record KeywordFile(string CldrVersion, Dictionary<string, KeywordEntry> Emoji);

    private sealed record KeywordEntry(string Name, IReadOnlyList<string> Keywords);
}
