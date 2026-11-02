// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private const string ExePath = @"C:\Users\Me\AppData\Local\FolderSwitch\current\FolderSwitch.exe";

    private static readonly Hotkey CtrlShiftF = new(KeyModifiers.Ctrl | KeyModifiers.Shift, 'F');
    private static readonly Hotkey CtrlAltF = new(KeyModifiers.Ctrl | KeyModifiers.Alt, 'F');
    private static readonly Hotkey WinL = new(KeyModifiers.Win, VirtualKeys.L);

    private readonly TempDirectory _temp = new();
    private readonly AppConfig _config;
    private readonly Localizer _localizer = TestLocalizer.Create();
    private readonly FakeHotkeyService _hotkeys = new();
    private readonly FakeAutostart _autostart = new() { IsEnabled = true };
    private readonly FakeUserPrompts _prompts = new();
    private readonly FakeConfigPersistence _persistence = new();
    private readonly string _storeDirectory;
    private readonly ConfigStore _store;

    public SettingsViewModelTests()
    {
        _config = DefaultConfigs.Create(StartContent.SampleTab, "Current", "Group");
        _config.Settings.FirstRunCompleted = true;
        _storeDirectory = _temp.File("store");
        _store = new ConfigStore(_storeDirectory, TimeProvider.System);
        _store.Save(_config);
        _hotkeys.Register(_config.Settings.Hotkey);
        _hotkeys.RegisterCalls.Clear();
    }

    public void Dispose() => _temp.Dispose();

    private SettingsViewModel Create(Func<Task>? checkUpdates = null)
    {
        var editor = new HotkeyEditorViewModel(_config.Settings.Hotkey, _hotkeys, _localizer);
        return new SettingsViewModel(
            _config, _localizer, editor, _hotkeys, _autostart, ExePath, _store, _prompts, _persistence, checkUpdates);
    }

    private static List<string?> RecordChanges(INotifyPropertyChanged source)
    {
        var names = new List<string?>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    private void Capture(SettingsViewModel vm, Hotkey h)
    {
        vm.Hotkey.StartCapture();
        _hotkeys.Press(h);
    }

    private string WriteImportFile(AppConfig config)
    {
        var path = _temp.File(Guid.NewGuid().ToString("N") + ".json");
        _store.Export(config, path);
        return path;
    }

    // ----- Language -----

    [Fact]
    public void Language_change_is_immediate()
    {
        var vm = Create();
        Assert.Equal([new LanguageOption("en", "English"), new LanguageOption("fr", "Français")], vm.Languages);
        Assert.Equal("en", vm.Language);
        var changes = RecordChanges(vm);

        vm.Language = "fr";

        Assert.Equal("fr", _localizer.Code);
        Assert.Equal("Configurer", _localizer["Window.Customize"]);
        Assert.Equal("fr", vm.Language);
        Assert.Equal("fr", _config.Settings.Language);
        Assert.Contains(nameof(SettingsViewModel.Language), changes);
        Assert.Same(_config, Assert.Single(_persistence.Saved));
    }

    [Theory]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_language_is_ignored(string? code)
    {
        var vm = Create();

        vm.Language = code!;

        Assert.Equal("en", _localizer.Code);
        Assert.Null(_config.Settings.Language);
        Assert.Equal(0, _persistence.SaveCount);
    }

    [Fact]
    public void Language_is_stored_as_spelled_by_its_file()
    {
        var vm = Create();

        vm.Language = "FR";
        Assert.Equal("fr", _config.Settings.Language);

        vm.Language = "fr";
        Assert.Equal(1, _persistence.SaveCount);
    }

    [Fact]
    public void Choosing_the_language_already_shown_pins_it_once()
    {
        var vm = Create();

        vm.Language = "en";
        vm.Language = "en";

        Assert.Equal("en", _config.Settings.Language);
        Assert.Equal(1, _persistence.SaveCount);
    }

    // ----- Hotkey -----

    [Fact]
    public void Hotkey_with_blocking_issue_cannot_be_applied()
    {
        var vm = Create();
        var canExecuteChanged = 0;
        vm.ApplyHotkeyCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        Capture(vm, WinL);

        Assert.False(vm.Hotkey.CanApply);
        Assert.False(vm.ApplyHotkeyCommand.CanExecute(null));
        Assert.True(canExecuteChanged > 0);

        vm.ApplyHotkey();

        Assert.Empty(_hotkeys.RegisterCalls);
        Assert.Equal(Hotkey.Default, _config.Settings.Hotkey);
        Assert.Equal(0, _persistence.SaveCount);

        Capture(vm, new Hotkey(KeyModifiers.Ctrl, null));
        Assert.False(vm.ApplyHotkeyCommand.CanExecute(null));
    }

    [Fact]
    public void CtrlAlt_warning_does_not_block()
    {
        var vm = Create();

        Capture(vm, CtrlAltF);

        Assert.Equal([HotkeyIssue.CtrlAltLikeAltGr], vm.Hotkey.Issues);
        Assert.True(vm.ApplyHotkeyCommand.CanExecute(null));

        vm.ApplyHotkeyCommand.Execute(null);

        Assert.Equal([CtrlAltF], _hotkeys.RegisterCalls);
        Assert.Equal(CtrlAltF, _config.Settings.Hotkey);
        Assert.Same(_config, Assert.Single(_persistence.Saved));
        Assert.Empty(_prompts.Errors);
    }

    // ----- Apply: only a combination that is not in effect yet -----

    [Fact]
    public void The_shortcut_in_effect_has_nothing_to_apply()
    {
        var vm = Create();

        Assert.True(vm.Hotkey.IsInEffect);
        Assert.True(vm.IsHotkeyApplied);
        Assert.False(vm.ApplyHotkeyCommand.CanExecute(null));

        vm.ApplyHotkey();

        Assert.Empty(_hotkeys.RegisterCalls);
        Assert.Equal(0, _persistence.SaveCount);
    }

    [Fact]
    public void Another_combination_can_be_applied_and_then_shows_as_applied()
    {
        var vm = Create();
        var changes = RecordChanges(vm);

        Capture(vm, CtrlShiftF);

        Assert.False(vm.Hotkey.IsInEffect);
        Assert.False(vm.IsHotkeyApplied);
        Assert.True(vm.ApplyHotkeyCommand.CanExecute(null));
        Assert.Contains(nameof(SettingsViewModel.IsHotkeyApplied), changes);
        changes.Clear();

        vm.ApplyHotkey();

        Assert.True(vm.IsHotkeyApplied);
        Assert.False(vm.ApplyHotkeyCommand.CanExecute(null));
        Assert.Contains(nameof(SettingsViewModel.IsHotkeyApplied), changes);
    }

    [Fact]
    public void Going_back_to_the_shortcut_in_effect_has_nothing_to_apply()
    {
        var vm = Create();
        Capture(vm, CtrlShiftF);

        vm.Hotkey.ResetToDefault();

        Assert.True(vm.IsHotkeyApplied);
        Assert.False(vm.ApplyHotkeyCommand.CanExecute(null));
    }

    [Fact]
    public void A_shortcut_refused_at_startup_can_be_applied_again()
    {
        var vm = Create();

        vm.IsHotkeyRegistered = false;

        Assert.False(vm.IsHotkeyApplied);
        Assert.True(vm.ApplyHotkeyCommand.CanExecute(null));

        // Nothing can be pressed: the test zone asks to apply the shortcut first.
        Assert.False(vm.Hotkey.CanTest);
        Assert.Equal("Apply this shortcut first, then try it here.", vm.Hotkey.TestPrompt);
        vm.Hotkey.NotifyTriggered();
        Assert.False(vm.Hotkey.TestRecognized);

        vm.ApplyHotkey();

        Assert.Equal([Hotkey.Default], _hotkeys.RegisterCalls);
        Assert.True(vm.IsHotkeyRegistered);
        Assert.True(vm.IsHotkeyApplied);
        Assert.True(vm.Hotkey.CanTest);
        Assert.Equal(0, _persistence.SaveCount);
    }

    [Fact]
    public void A_refused_registration_shows_nothing_as_applied()
    {
        var vm = Create();
        Capture(vm, CtrlShiftF);
        _hotkeys.Taken.Add(CtrlShiftF);

        vm.ApplyHotkey();

        Assert.False(vm.IsHotkeyApplied);
    }

    // ----- Test zone: only the shortcut in effect can be pressed -----

    [Fact]
    public void Test_zone_recognizes_the_shortcut_in_effect()
    {
        var vm = Create();

        Assert.True(vm.Hotkey.CanTest);
        Assert.Equal("Try the shortcut: press Ctrl + ⊞ Win", vm.Hotkey.TestPrompt);
        vm.Hotkey.NotifyTriggered();
        Assert.True(vm.Hotkey.TestRecognized);
    }

    [Fact]
    public void Test_zone_asks_to_apply_a_combination_shown_but_not_in_effect()
    {
        var vm = Create();
        var changes = RecordChanges(vm.Hotkey);

        Capture(vm, CtrlShiftF);

        Assert.False(vm.Hotkey.CanTest);
        Assert.Equal("Apply this shortcut first, then try it here.", vm.Hotkey.TestPrompt);
        Assert.Contains(nameof(HotkeyEditorViewModel.TestPrompt), changes);

        // A press can only be the old shortcut, still in effect: it does not confirm the new one.
        vm.Hotkey.NotifyTriggered();
        Assert.False(vm.Hotkey.TestRecognized);
    }

    [Fact]
    public void Test_zone_tests_the_combination_once_it_is_applied()
    {
        var vm = Create();
        Capture(vm, CtrlShiftF);

        vm.ApplyHotkey();

        Assert.True(vm.Hotkey.CanTest);
        Assert.Equal("Try the shortcut: press Ctrl + Shift + F", vm.Hotkey.TestPrompt);
        vm.Hotkey.NotifyTriggered();
        Assert.True(vm.Hotkey.TestRecognized);
    }

    [Fact]
    public void Test_zone_still_asks_to_apply_after_a_refused_registration()
    {
        var vm = Create();
        Capture(vm, CtrlShiftF);
        _hotkeys.Taken.Add(CtrlShiftF);

        vm.ApplyHotkey();

        Assert.False(vm.Hotkey.CanTest);
    }

    [Fact]
    public void Test_zone_tests_the_imported_shortcut()
    {
        var vm = Create();
        var imported = DefaultConfigs.Create(StartContent.EmptyTab, "Imported", "Group");
        imported.Settings.Hotkey = CtrlShiftF;
        _prompts.ImportPathToPick = WriteImportFile(imported);

        vm.Import();

        Assert.Equal(CtrlShiftF, vm.Hotkey.Current);
        Assert.True(vm.Hotkey.CanTest);
    }

    [Fact]
    public void ApplyHotkey_failure_keeps_the_old_setting()
    {
        var vm = Create();
        Capture(vm, CtrlShiftF);
        Assert.True(vm.Hotkey.CanApply);
        _hotkeys.Taken.Add(CtrlShiftF); // another application took it in the meantime

        vm.ApplyHotkey();

        Assert.Equal([CtrlShiftF], _hotkeys.RegisterCalls);
        Assert.Equal(["Hotkey.Issue.TakenByOtherApp"], _prompts.Errors);
        Assert.Equal(Hotkey.Default, _config.Settings.Hotkey);
        Assert.Equal(0, _persistence.SaveCount);
    }

    [Fact]
    public void Refused_hotkey_shows_why_in_the_editor()
    {
        var vm = Create();
        Capture(vm, CtrlShiftF);
        _hotkeys.Taken.Add(CtrlShiftF); // another application took it in the meantime
        var canExecuteChanged = 0;
        vm.ApplyHotkeyCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.ApplyHotkey();

        Assert.Equal(CtrlShiftF, vm.Hotkey.Current);
        Assert.Equal([HotkeyIssue.TakenByOtherApp], vm.Hotkey.Issues);
        Assert.False(vm.Hotkey.CanApply);
        Assert.True(canExecuteChanged > 0);
        Assert.False(vm.ApplyHotkeyCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyHotkey_that_throws_keeps_the_old_setting()
    {
        var vm = Create();
        Capture(vm, CtrlShiftF);
        _hotkeys.RegisterThrows = new Win32Exception(5);

        vm.ApplyHotkey();

        Assert.Equal(["Error.ActionFailed"], _prompts.Errors);
        Assert.Equal(Hotkey.Default, _config.Settings.Hotkey);
        Assert.Equal(0, _persistence.SaveCount);
    }

    // ----- Start with Windows -----

    [Fact]
    public void StartWithWindows_shows_and_changes_the_system_setting()
    {
        var vm = Create();
        Assert.True(vm.StartWithWindows);

        vm.StartWithWindows = false;

        Assert.Equal([(false, ExePath)], _autostart.Calls);
        Assert.False(_config.Settings.StartWithWindows);
        Assert.Equal(1, _persistence.SaveCount);

        vm.StartWithWindows = true;

        Assert.Equal((true, ExePath), _autostart.Calls[^1]);
        Assert.True(_config.Settings.StartWithWindows);
        Assert.Equal(2, _persistence.SaveCount);
    }

    [Fact]
    public void StartWithWindows_exception_reverts()
    {
        var vm = Create();
        _autostart.Throws = new UnauthorizedAccessException();
        var changes = RecordChanges(vm);

        vm.StartWithWindows = false;

        Assert.Equal(["Error.ActionFailed"], _prompts.Errors);
        Assert.True(vm.StartWithWindows);
        Assert.Contains(nameof(SettingsViewModel.StartWithWindows), changes);
        Assert.True(_config.Settings.StartWithWindows);
        Assert.Equal(0, _persistence.SaveCount);
    }

    // ----- Export -----

    [Fact]
    public void Export_writes_the_configuration_to_the_chosen_file()
    {
        var vm = Create();
        var path = _temp.File("export.json");
        _prompts.ExportPathToPick = path;

        vm.ExportCommand.Execute(null);

        Assert.Equal("Current", _store.Import(path).Tabs.Single().Name);
        Assert.Empty(_prompts.Errors);
    }

    [Fact]
    public void Export_cancelled_writes_nothing()
    {
        var vm = Create();

        vm.Export();

        Assert.Empty(Directory.GetFiles(_temp.Path));
        Assert.Empty(_prompts.Errors);
    }

    [Fact]
    public void Export_failure_shows_an_error()
    {
        var vm = Create();
        _prompts.ExportPathToPick = Path.Combine(_temp.Path, "missing", "export.json");

        vm.Export();

        Assert.Equal(["Error.ActionFailed"], _prompts.Errors);
    }

    // ----- Import -----

    [Fact]
    public void Import_backs_up_before_replacing()
    {
        var vm = Create();
        _prompts.ImportPathToPick = WriteImportFile(DefaultConfigs.Create(StartContent.EmptyTab, "Imported", "unused"));
        var backups = Path.Combine(_storeDirectory, "backups");
        string[]? backupsWhenSaved = null;
        _persistence.OnSave = _ => backupsWhenSaved = Directory.Exists(backups) ? Directory.GetFiles(backups) : [];

        vm.ImportCommand.Execute(null);

        var backup = Assert.Single(backupsWhenSaved!);
        Assert.Equal("Current", _store.Import(backup).Tabs.Single().Name);
        Assert.Equal("Imported", Assert.Single(_persistence.Saved).Tabs.Single().Name);
    }

    [Fact]
    public void ConfigReplaced_is_raised_with_the_imported_configuration()
    {
        var vm = Create();
        _prompts.ImportPathToPick = WriteImportFile(DefaultConfigs.Create(StartContent.EmptyTab, "Imported", "unused"));
        var replaced = new List<AppConfig>();
        vm.ConfigReplaced += (_, c) => replaced.Add(c);

        vm.Import();

        var imported = Assert.Single(replaced);
        Assert.Equal("Imported", imported.Tabs.Single().Name);
        Assert.Same(imported, _persistence.Saved.Single());
        Assert.Empty(_prompts.Errors);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Import_failure_replaces_nothing(bool missingFile)
    {
        var vm = Create();
        var path = _temp.File("bad.json");
        if (!missingFile)
        {
            File.WriteAllText(path, "{ not a configuration");
        }

        _prompts.ImportPathToPick = path;
        var replaced = 0;
        vm.ConfigReplaced += (_, _) => replaced++;

        vm.Import();

        Assert.Equal(["Error.ActionFailed"], _prompts.Errors);
        Assert.Equal(0, _persistence.SaveCount);
        Assert.Equal(0, replaced);

        // The current configuration is still the one edited.
        vm.Language = "fr";
        Assert.Same(_config, Assert.Single(_persistence.Saved));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_import_creates_no_backup(bool missingFile)
    {
        var vm = Create();
        var path = _temp.File("bad.json");
        if (!missingFile)
        {
            File.WriteAllText(path, "{ not a configuration");
        }

        _prompts.ImportPathToPick = path;

        vm.Import();

        // A backup is only made for a replacement: a failure must not push the oldest backups out.
        Assert.False(Directory.Exists(Path.Combine(_storeDirectory, "backups")));
    }

    [Fact]
    public void Import_cancelled_does_nothing()
    {
        var vm = Create();

        vm.Import();

        Assert.False(Directory.Exists(Path.Combine(_storeDirectory, "backups")));
        Assert.Equal(0, _persistence.SaveCount);
        Assert.Empty(_prompts.Errors);
    }

    [Fact]
    public void Later_changes_edit_the_imported_configuration()
    {
        var vm = Create();
        _prompts.ImportPathToPick = WriteImportFile(DefaultConfigs.Create(StartContent.EmptyTab, "Imported", "unused"));
        vm.Import();

        vm.Language = "fr";
        vm.StartWithWindows = false;

        Assert.All(_persistence.Saved, saved => Assert.Equal("Imported", saved.Tabs.Single().Name));
        Assert.Equal("fr", _persistence.Saved[^1].Settings.Language);
        Assert.Null(_config.Settings.Language);
        Assert.True(_config.Settings.StartWithWindows);
    }

    [Fact]
    public void Import_applies_the_imported_preferences_and_keeps_the_state_of_this_PC()
    {
        var vm = Create();
        var other = DefaultConfigs.Create(StartContent.EmptyTab, "Imported", "unused");
        other.Settings.Language = "fr";
        other.Settings.Hotkey = CtrlShiftF;
        other.Settings.StartWithWindows = false;
        other.Settings.FirstRunCompleted = false;
        _prompts.ImportPathToPick = WriteImportFile(other);
        var changes = RecordChanges(vm);

        vm.Import();

        var saved = Assert.Single(_persistence.Saved);
        Assert.Equal("fr", _localizer.Code);
        Assert.Equal("fr", vm.Language);
        Assert.Contains(nameof(SettingsViewModel.Language), changes);
        Assert.Equal([CtrlShiftF], _hotkeys.RegisterCalls);
        Assert.Equal(CtrlShiftF, vm.Hotkey.Current);
        Assert.Equal(CtrlShiftF, saved.Settings.Hotkey);

        // Whether FolderSwitch starts with Windows, and whether its wizard ran, belong to this PC.
        Assert.Empty(_autostart.Calls);
        Assert.True(saved.Settings.StartWithWindows);
        Assert.True(vm.StartWithWindows);
        Assert.True(saved.Settings.FirstRunCompleted);
    }

    [Fact]
    public void Import_with_a_hotkey_unavailable_here_keeps_the_current_one()
    {
        var vm = Create();
        var other = DefaultConfigs.Create(StartContent.EmptyTab, "Imported", "unused");
        other.Settings.Hotkey = CtrlShiftF;
        _hotkeys.Taken.Add(CtrlShiftF);
        _prompts.ImportPathToPick = WriteImportFile(other);
        var replaced = 0;
        vm.ConfigReplaced += (_, _) => replaced++;

        vm.Import();

        Assert.Equal(["Hotkey.Issue.TakenByOtherApp"], _prompts.Errors);
        Assert.Equal(Hotkey.Default, Assert.Single(_persistence.Saved).Settings.Hotkey);
        Assert.Equal(Hotkey.Default, vm.Hotkey.Current);
        Assert.Equal(1, replaced);
    }

    // ----- Updates -----

    [Fact]
    public void CheckUpdates_is_disabled_without_an_update_service()
    {
        var vm = Create();

        Assert.False(vm.CheckUpdatesCommand.CanExecute(null));
    }

    [Fact]
    public async Task CheckUpdates_runs_the_update_check()
    {
        var calls = 0;
        var vm = Create(() =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
        await vm.CheckUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void CheckUpdates_is_disabled_while_an_update_operation_is_running()
    {
        var vm = Create(() => Task.CompletedTask);
        var changed = 0;
        vm.CheckUpdatesCommand.CanExecuteChanged += (_, _) => changed++;

        vm.IsUpdateBusy = true;

        Assert.False(vm.CheckUpdatesCommand.CanExecute(null));
        Assert.Equal(1, changed);

        vm.IsUpdateBusy = false;

        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
        Assert.Equal(2, changed);
    }

    [Fact]
    public void An_update_operation_does_not_enable_a_check_that_does_not_exist()
    {
        var vm = Create();

        vm.IsUpdateBusy = true;
        vm.IsUpdateBusy = false;

        Assert.False(vm.CheckUpdatesCommand.CanExecute(null));
    }
}
