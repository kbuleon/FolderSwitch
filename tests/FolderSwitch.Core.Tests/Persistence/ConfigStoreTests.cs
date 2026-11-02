// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;

namespace FolderSwitch.Core.Tests.Persistence;

public class ConfigStoreTests : IDisposable
{
    // 10:00:00Z is 12:00:00 in the fake clock's local zone (UTC+02:00).
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 10, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory temp = new();
    private readonly FakeTimeProvider clock = new(Start);
    private readonly ConfigStore store;
    private readonly string directory;

    public ConfigStoreTests()
    {
        directory = Path.Combine(temp.Root, "store");
        store = new ConfigStore(directory, clock);
    }

    private string ConfigPath => Path.Combine(directory, "config.json");

    private string BackupsDirectory => Path.Combine(directory, "backups");

    public void Dispose() => temp.Dispose();

    private static Func<AppConfig> MustNotCreateDefault() =>
        () => throw new InvalidOperationException("createDefault must not be called.");

    private void WriteBackup(string fileName, string content)
    {
        Directory.CreateDirectory(BackupsDirectory);
        File.WriteAllText(Path.Combine(BackupsDirectory, fileName), content);
    }

    private string[] BackupNames() =>
        Directory.Exists(BackupsDirectory)
            ? Directory.GetFiles(BackupsDirectory).Select(f => Path.GetFileName(f)!).Order(StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    // ----- Construction -----

    [Fact]
    public void Constructor_creates_the_directory_when_missing()
    {
        var nested = Path.Combine(temp.Root, "a", "b", "FolderSwitch");

        _ = new ConfigStore(nested, clock);

        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void DefaultDirectory_is_FolderSwitch_under_AppData()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FolderSwitch");

        Assert.Equal(expected, ConfigStore.DefaultDirectory);
    }

    // ----- Load -----

    [Fact]
    public void Load_missing_file_creates_default()
    {
        var created = TestConfigs.Minimal("Default");

        var result = store.Load(() => created);

        Assert.Equal(LoadOutcome.CreatedNew, result.Outcome);
        Assert.Same(created, result.Config);
        Assert.True(File.Exists(ConfigPath));
        TestConfigs.AssertEquivalent(created, ConfigSerializer.Deserialize(File.ReadAllText(ConfigPath)));
    }

    [Fact]
    public void Load_valid_file_returns_it_without_creating_a_default()
    {
        var saved = TestConfigs.Rich();
        store.Save(saved);

        var result = store.Load(MustNotCreateDefault());

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        TestConfigs.AssertEquivalent(saved, result.Config);
    }

    [Fact]
    public void Load_corrupt_file_restores_latest_valid_backup()
    {
        const string corrupt = "{ \"tabs\": [";
        File.WriteAllText(ConfigPath, corrupt);
        WriteBackup("config-20261030-090000.json", ConfigSerializer.Serialize(TestConfigs.Minimal("Oldest")));
        WriteBackup("config-20261030-100000.json", ConfigSerializer.Serialize(TestConfigs.Minimal("Middle")));
        WriteBackup("config-20261030-110000.json", "{ this backup is broken too");

        var result = store.Load(MustNotCreateDefault());

        Assert.Equal(LoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal("Middle", Assert.Single(result.Config.Tabs).Name);
        var invalid = Assert.Single(Directory.GetFiles(directory, "config.invalid-*.json"));
        Assert.Equal(corrupt, File.ReadAllText(invalid));
        // The restored configuration is now the live one.
        Assert.Equal("Middle", Assert.Single(ConfigSerializer.Deserialize(File.ReadAllText(ConfigPath)).Tabs).Name);
    }

    [Fact]
    public void Load_corrupt_without_backup_resets()
    {
        const string corrupt = "{ \"tabs\": [";
        File.WriteAllText(ConfigPath, corrupt);
        var fresh = TestConfigs.Minimal("Fresh");

        var result = store.Load(() => fresh);

        Assert.Equal(LoadOutcome.ResetToDefault, result.Outcome);
        Assert.Same(fresh, result.Config);
        Assert.Equal(corrupt, File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "config.invalid-*.json"))));
        Assert.Equal("Fresh", Assert.Single(ConfigSerializer.Deserialize(File.ReadAllText(ConfigPath)).Tabs).Name);
    }

    [Fact]
    public void Load_with_only_invalid_backups_resets()
    {
        File.WriteAllText(ConfigPath, "garbage");
        WriteBackup("config-20261030-100000.json", "also garbage");

        var result = store.Load(() => TestConfigs.Minimal());

        Assert.Equal(LoadOutcome.ResetToDefault, result.Outcome);
    }

    [Fact]
    public void Load_file_with_newer_schema_is_treated_as_invalid()
    {
        const string newer = """{ "schemaVersion": 3, "tabs": [ { "name": "Future" } ] }""";
        File.WriteAllText(ConfigPath, newer);
        WriteBackup("config-20261030-100000.json", ConfigSerializer.Serialize(TestConfigs.Minimal("Backup")));

        var result = store.Load(MustNotCreateDefault());

        Assert.Equal(LoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal(newer, File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "config.invalid-*.json"))));
    }

    [Fact]
    public void Load_names_the_invalid_file_with_the_local_timestamp()
    {
        File.WriteAllText(ConfigPath, "garbage");

        store.Load(() => TestConfigs.Minimal());

        Assert.True(File.Exists(Path.Combine(directory, "config.invalid-20261030-120000.json")));
    }

    [Fact]
    public void Load_keeps_every_invalid_file_when_corruption_happens_twice_in_the_same_second()
    {
        File.WriteAllText(ConfigPath, "garbage one");
        store.Load(() => TestConfigs.Minimal());
        File.WriteAllText(ConfigPath, "garbage two");
        store.Load(() => TestConfigs.Minimal());

        var contents = Directory.GetFiles(directory, "config.invalid-*.json").Select(File.ReadAllText).Order().ToArray();

        Assert.Equal(new[] { "garbage one", "garbage two" }, contents);
    }

    // ----- Save -----

    [Fact]
    public void Save_leaves_no_tmp_file_and_is_readable()
    {
        var config = TestConfigs.Rich();

        store.Save(config);
        store.Save(config);

        Assert.False(File.Exists(ConfigPath + ".tmp"));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        TestConfigs.AssertEquivalent(config, ConfigSerializer.Deserialize(File.ReadAllText(ConfigPath)));
    }

    [Fact]
    public void Save_writes_UTF8_without_a_byte_order_mark()
    {
        var config = TestConfigs.Minimal("Réseau é");

        store.Save(config);

        var bytes = File.ReadAllBytes(ConfigPath);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "The file starts with a byte-order mark.");
        Assert.Equal(ConfigSerializer.Serialize(config), System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Save_replaces_the_previous_content()
    {
        store.Save(TestConfigs.Minimal("First"));

        store.Save(TestConfigs.Minimal("Second"));

        Assert.Equal("Second", Assert.Single(ConfigSerializer.Deserialize(File.ReadAllText(ConfigPath)).Tabs).Name);
    }

    [Fact]
    public void Save_recreates_the_directory_if_it_was_removed()
    {
        Directory.Delete(directory, recursive: true);

        store.Save(TestConfigs.Minimal());

        Assert.True(File.Exists(ConfigPath));
    }

    // ----- BackupOnStartup -----

    [Fact]
    public void BackupOnStartup_copies_config_into_backups_with_a_local_time_name()
    {
        store.Save(TestConfigs.Rich());

        store.BackupOnStartup();

        var name = Assert.Single(BackupNames());
        Assert.Equal("config-20261030-120000.json", name);
        Assert.Equal(File.ReadAllText(ConfigPath), File.ReadAllText(Path.Combine(BackupsDirectory, name)));
    }

    [Fact]
    public void BackupOnStartup_without_a_config_file_does_nothing()
    {
        store.BackupOnStartup();

        Assert.Empty(BackupNames());
    }

    [Fact]
    public void BackupOnStartup_keeps_10_most_recent()
    {
        store.Save(TestConfigs.Minimal());
        Directory.CreateDirectory(BackupsDirectory);
        File.WriteAllText(Path.Combine(BackupsDirectory, "notes.txt"), "not a backup");

        for (var i = 0; i < 12; i++)
        {
            store.BackupOnStartup();
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var backups = BackupNames().Where(n => n.StartsWith("config-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(10, backups.Length);
        Assert.DoesNotContain("config-20261030-120000.json", backups);
        Assert.DoesNotContain("config-20261030-120001.json", backups);
        Assert.Contains("config-20261030-120002.json", backups);
        Assert.Contains("config-20261030-120011.json", backups);
        // Files that are not backups are never touched.
        Assert.Contains("notes.txt", BackupNames());
    }

    [Fact]
    public void BackupOnStartup_twice_in_the_same_second_keeps_both_and_the_later_one_is_newer()
    {
        store.Save(TestConfigs.Minimal("First"));
        store.BackupOnStartup();
        store.Save(TestConfigs.Minimal("Second"));
        store.BackupOnStartup();

        Assert.Equal(2, BackupNames().Length);

        File.WriteAllText(ConfigPath, "garbage");
        var result = store.Load(MustNotCreateDefault());

        Assert.Equal(LoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal("Second", Assert.Single(result.Config.Tabs).Name);
    }

    [Fact]
    public void A_backup_name_whose_suffix_overflows_is_skipped_never_read_nor_deleted()
    {
        // A suffix past int.MaxValue: FolderSwitch never writes it, and it must not make the load fail.
        const string odd = "config-20261030-110000-99999999999.json";
        WriteBackup(odd, ConfigSerializer.Serialize(TestConfigs.Minimal("Odd")));
        WriteBackup("config-20261030-100000.json", ConfigSerializer.Serialize(TestConfigs.Minimal("Valid")));
        File.WriteAllText(ConfigPath, "garbage");

        var result = store.Load(MustNotCreateDefault());

        Assert.Equal(LoadOutcome.RestoredFromBackup, result.Outcome);
        Assert.Equal("Valid", Assert.Single(result.Config.Tabs).Name);

        // Pruning ignores it too.
        for (var i = 0; i < 11; i++)
        {
            store.BackupOnStartup();
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.Contains(odd, BackupNames());
    }

    // ----- Export / Import -----

    [Fact]
    public void Export_leaves_no_temporary_file()
    {
        var path = Path.Combine(temp.Root, "export.json");

        store.Export(TestConfigs.Rich(), path);

        Assert.Equal(["export.json"], Directory.GetFiles(temp.Root).Select(Path.GetFileName).Where(n => n!.StartsWith("export", StringComparison.Ordinal)));
        Assert.Empty(Directory.GetFiles(temp.Root, "*.tmp"));
    }

    [Fact]
    public void Export_over_an_existing_file_replaces_it_whole()
    {
        var path = Path.Combine(temp.Root, "export.json");
        File.WriteAllText(path, new string('x', 100_000));

        store.Export(TestConfigs.Minimal("Exported"), path);

        Assert.Equal("Exported", Assert.Single(store.Import(path).Tabs).Name);
    }

    [Fact]
    public void A_failed_export_leaves_the_existing_file_unchanged_and_no_temporary_file()
    {
        var path = Path.Combine(temp.Root, "export.json");
        File.WriteAllText(path, "previous export");
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            var error = Record.Exception(() => store.Export(TestConfigs.Rich(), path));

            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
            Assert.Equal("previous export", File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(temp.Root, "*.tmp"));
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Export_then_Import_roundtrips()
    {
        var original = TestConfigs.Rich();
        var path = Path.Combine(temp.Root, "export.json");

        store.Export(original, path);
        var imported = store.Import(path);

        TestConfigs.AssertEquivalent(original, imported);
    }

    [Fact]
    public void Import_invalid_file_throws_and_does_not_touch_current()
    {
        store.Save(TestConfigs.Rich());
        var before = File.ReadAllBytes(ConfigPath);
        var filesBefore = Directory.GetFileSystemEntries(directory).Order().ToArray();
        var bad = Path.Combine(temp.Root, "bad.json");
        File.WriteAllText(bad, "{ \"schemaVersion\": 3, \"tabs\": [] }");

        Assert.Throws<ConfigFormatException>(() => store.Import(bad));

        Assert.Equal(before, File.ReadAllBytes(ConfigPath));
        Assert.Equal(filesBefore, Directory.GetFileSystemEntries(directory).Order().ToArray());
    }
}
