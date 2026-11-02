// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Persistence;

/// <summary>
/// Keeps the configuration in a directory (by default <c>%APPDATA%\FolderSwitch</c>): <c>config.json</c>,
/// rolling backups in <c>backups\</c>, and the invalid files set aside by <see cref="Load"/>.
/// File names carry the local time of the injected clock.
/// </summary>
public sealed partial class ConfigStore
{
    private const string ConfigFileName = "config.json";
    private const string BackupsDirectoryName = "backups";
    private const string TimestampFormat = "yyyyMMdd-HHmmss";
    private const int MaxBackups = 10;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string directory;
    private readonly TimeProvider clock;

    public ConfigStore(string directory, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(clock);

        this.directory = directory;
        this.clock = clock;
        Directory.CreateDirectory(directory);
    }

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderSwitch");

    private string ConfigPath => Path.Combine(directory, ConfigFileName);

    private string BackupsDirectory => Path.Combine(directory, BackupsDirectoryName);

    /// <summary>
    /// Loads <c>config.json</c>. A missing file is created from <paramref name="createDefault"/>. An invalid
    /// file (see <see cref="ConfigSerializer.Deserialize"/>) is kept as <c>config.invalid-&lt;timestamp&gt;.json</c>,
    /// then replaced by the latest valid backup or, failing that, by <paramref name="createDefault"/>.
    /// An invalid configuration never makes this method throw.
    /// </summary>
    public LoadResult Load(Func<AppConfig> createDefault)
    {
        ArgumentNullException.ThrowIfNull(createDefault);

        if (!File.Exists(ConfigPath))
        {
            return CreateAndSave(createDefault, LoadOutcome.CreatedNew);
        }

        if (TryRead(ConfigPath, out var config))
        {
            return new LoadResult(config, LoadOutcome.Loaded);
        }

        // Copy rather than move: config.json stays valid-or-replaced at every instant, even if we crash here.
        File.Copy(ConfigPath, UniquePath("config.invalid", ".json", directory));

        foreach (var backup in ListBackups())
        {
            if (TryRead(backup.Path, out var restored))
            {
                Save(restored);
                return new LoadResult(restored, LoadOutcome.RestoredFromBackup);
            }
        }

        return CreateAndSave(createDefault, LoadOutcome.ResetToDefault);
    }

    /// <summary>
    /// Writes <c>config.json.tmp</c>, flushed to the disk, then atomically replaces <c>config.json</c>: no partial file is
    /// ever visible, and a power loss right after the replace cannot leave a file whose content never reached the disk.
    /// </summary>
    public void Save(AppConfig config)
    {
        var json = ConfigSerializer.Serialize(config);
        Directory.CreateDirectory(directory);
        var temporaryPath = ConfigPath + ".tmp";
        WriteDurably(temporaryPath, json);
        File.Move(temporaryPath, ConfigPath, overwrite: true);
    }

    /// <summary>
    /// Copies <c>config.json</c> to <c>backups\config-yyyyMMdd-HHmmss.json</c> and deletes all but the
    /// 10 most recent backups. Does nothing when there is no <c>config.json</c>.
    /// </summary>
    public void BackupOnStartup()
    {
        if (!File.Exists(ConfigPath))
        {
            return;
        }

        Directory.CreateDirectory(BackupsDirectory);
        File.Copy(ConfigPath, UniquePath("config", ".json", BackupsDirectory));

        foreach (var obsolete in ListBackups().Skip(MaxBackups))
        {
            File.Delete(obsolete.Path);
        }
    }

    /// <summary>
    /// Writes the configuration to <paramref name="path"/> the way <see cref="Save"/> writes <c>config.json</c>: to a
    /// temporary file next to it, flushed, then moved over it. A failed export (a full disk, a file that cannot be
    /// replaced) leaves no partial file, and a file that was already there unchanged.
    /// </summary>
    public void Export(AppConfig config, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var json = ConfigSerializer.Serialize(config);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        try
        {
            WriteDurably(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temporaryPath);
            throw;
        }
    }

    /// <summary>Reads and validates a configuration file; nothing is changed in the store.</summary>
    /// <exception cref="ConfigFormatException">The file is not a valid configuration.</exception>
    public AppConfig Import(string path)
    {
        return ConfigSerializer.Deserialize(File.ReadAllText(path));
    }

    /// <summary>Writes <paramref name="text"/> in UTF-8 without a byte-order mark, then flushes it to the disk itself.</summary>
    private static void WriteDurably(string path, string text)
    {
        var bytes = Utf8NoBom.GetBytes(text);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A temporary file left behind is harmless; the failure that led here is what matters.
        }
    }

    private LoadResult CreateAndSave(Func<AppConfig> createDefault, LoadOutcome outcome)
    {
        var config = createDefault();
        Save(config);
        return new LoadResult(config, outcome);
    }

    private static bool TryRead(string path, out AppConfig config)
    {
        try
        {
            config = ConfigSerializer.Deserialize(File.ReadAllText(path));
            return true;
        }
        catch (ConfigFormatException)
        {
            config = null!;
            return false;
        }
    }

    /// <summary>
    /// Backups, newest first. Ordered by the timestamp in the name, then by the same-second suffix. A file whose suffix is
    /// not a number FolderSwitch could have written (too large) is not a backup: it is left alone, never read nor deleted.
    /// </summary>
    private List<(string Path, string Timestamp, int Sequence)> ListBackups()
    {
        var backups = new List<(string Path, string Timestamp, int Sequence)>();
        if (!Directory.Exists(BackupsDirectory))
        {
            return backups;
        }

        foreach (var path in Directory.EnumerateFiles(BackupsDirectory))
        {
            var match = BackupNamePattern().Match(Path.GetFileName(path));
            if (!match.Success)
            {
                continue;
            }

            var sequence = 1;
            if (match.Groups["n"].Success
                && !int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out sequence))
            {
                continue;
            }

            backups.Add((path, match.Groups["ts"].Value, sequence));
        }

        backups.Sort((a, b) =>
        {
            var byTimestamp = string.CompareOrdinal(b.Timestamp, a.Timestamp);
            return byTimestamp != 0 ? byTimestamp : b.Sequence.CompareTo(a.Sequence);
        });
        return backups;
    }

    /// <summary><c>{stem}-{now}{extension}</c>, or <c>{stem}-{now}-2{extension}</c>, <c>-3</c>... if that name is taken.</summary>
    private string UniquePath(string stem, string extension, string targetDirectory)
    {
        var timestamp = clock.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        var candidate = Path.Combine(targetDirectory, $"{stem}-{timestamp}{extension}");
        for (var sequence = 2; File.Exists(candidate); sequence++)
        {
            candidate = Path.Combine(targetDirectory, $"{stem}-{timestamp}-{sequence}{extension}");
        }

        return candidate;
    }

    [GeneratedRegex(@"^config-(?<ts>\d{8}-\d{6})(?:-(?<n>\d+))?\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex BackupNamePattern();
}
