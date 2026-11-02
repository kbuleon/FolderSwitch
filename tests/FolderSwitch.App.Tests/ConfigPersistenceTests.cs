// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.App.Services;
using FolderSwitch.App.Tests.Support;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;

namespace FolderSwitch.App.Tests;

/// <summary>Saving after every edit: a disk failure never fails the edit, and is reported once until a save succeeds.</summary>
public sealed class ConfigPersistenceTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly ConfigStore _store;
    private int _reports;

    public ConfigPersistenceTests()
    {
        _store = new ConfigStore(_temp.Root, TimeProvider.System);
    }

    private string ConfigPath => _temp.File("config.json");

    /// <summary>A folder where the temporary file goes makes every save fail, as a refused write would.</summary>
    private string Blocker => _temp.File("config.json.tmp");

    public void Dispose() => _temp.Dispose();

    private static AppConfig Config(string tabName) => DefaultConfigs.Create(StartContent.EmptyTab, tabName, "Group");

    [Fact]
    public void A_save_writes_the_configuration()
    {
        var persistence = new ConfigPersistence(_store, readOnly: false, () => _reports++);

        persistence.Save(Config("Saved"));

        Assert.Equal("Saved", Assert.Single(_store.Import(ConfigPath).Tabs).Name);
        Assert.Equal(0, _reports);
    }

    [Fact]
    public void A_read_only_session_never_writes()
    {
        File.WriteAllText(ConfigPath, "the file that could not be read");
        var persistence = new ConfigPersistence(_store, readOnly: true, () => _reports++);

        persistence.Save(Config("First"));
        persistence.Save(Config("Second"));

        Assert.Equal("the file that could not be read", File.ReadAllText(ConfigPath));
        Assert.Equal(0, _reports);
    }

    [Fact]
    public void A_failed_save_does_not_throw_and_is_reported_once()
    {
        Directory.CreateDirectory(Blocker);
        var persistence = new ConfigPersistence(_store, readOnly: false, () => _reports++);

        persistence.Save(Config("First"));
        persistence.Save(Config("Second"));
        persistence.Save(Config("Third"));

        Assert.Equal(1, _reports);
        Assert.False(File.Exists(ConfigPath));
    }

    [Fact]
    public void A_failure_after_a_successful_save_is_reported_again()
    {
        var persistence = new ConfigPersistence(_store, readOnly: false, () => _reports++);
        Directory.CreateDirectory(Blocker);
        persistence.Save(Config("Fails"));

        Directory.Delete(Blocker);
        persistence.Save(Config("Works"));
        Assert.Equal("Works", Assert.Single(_store.Import(ConfigPath).Tabs).Name);

        Directory.CreateDirectory(Blocker);
        persistence.Save(Config("Fails again"));

        Assert.Equal(2, _reports);
    }
}
