// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

public class WizardViewModelTests
{
    private const string ExePath = @"C:\Users\Me\AppData\Local\FolderSwitch\current\FolderSwitch.exe";

    private static readonly Hotkey CtrlShiftF = new(KeyModifiers.Ctrl | KeyModifiers.Shift, 'F');
    private static readonly Hotkey WinL = new(KeyModifiers.Win, VirtualKeys.L);

    private readonly Localizer _localizer = TestLocalizer.Create();
    private readonly FakeSystemInfo _system = new() { UiCultureName = "fr-FR" };
    private readonly FakeHotkeyService _hotkeys = new();
    private readonly FakeAutostart _autostart = new();
    private readonly FakeConfigPersistence _persistence = new();

    // The names the wizard gives the sample tab and group (the wizard starts in French here).
    private static readonly SynonymConcept[] SampleNames =
    [
        TestIcons.Concept("🐱", ("fr", ["exemple"])),
        TestIcons.Concept("🏖", ("fr", ["windows"])),
    ];

    private WizardViewModel Create() => new(_localizer, _system, _hotkeys, _autostart, ExePath, _persistence, TestIcons.Lazy(extra: SampleNames));

    private static void GoToStep(WizardViewModel vm, int step)
    {
        while (vm.Step < step)
        {
            vm.NextCommand.Execute(null);
        }
    }

    private AppConfig FinishAndCapture(WizardViewModel vm)
    {
        AppConfig? finished = null;
        vm.Finished += (_, c) => finished = c;
        GoToStep(vm, WizardViewModel.LastStep);
        vm.FinishCommand.Execute(null);
        return Assert.IsType<AppConfig>(finished);
    }

    [Fact]
    public void Wizard_defaults()
    {
        using var vm = Create();

        Assert.Equal(0, vm.Step);
        Assert.Equal([new LanguageOption("en", "English"), new LanguageOption("fr", "Français")], vm.Languages);
        Assert.Equal("fr", vm.Language);
        Assert.Equal("fr", _localizer.Code);
        Assert.Equal(Hotkey.Default, vm.Hotkey.Current);
        Assert.Equal(ThemeMode.Light, vm.Mode);
        Assert.Equal("fluent", vm.Theme);
        Assert.True(vm.StartWithWindows);
        Assert.Equal(StartContent.SampleTab, vm.StartContent);
        Assert.False(vm.AutostartFailed);
        Assert.True(vm.NextCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
        Assert.False(vm.FinishCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("")]
    public void Wizard_falls_back_to_english(string culture)
    {
        _system.UiCultureName = culture;

        using var vm = Create();

        Assert.Equal("en", vm.Language);
        Assert.Equal("en", _localizer.Code);
    }

    [Fact]
    public void Wizard_language_selection_updates_the_localizer()
    {
        using var vm = Create();
        var editorChanges = new List<string?>();
        vm.Hotkey.PropertyChanged += (_, e) => editorChanges.Add(e.PropertyName);

        vm.Language = "en";

        Assert.Equal("en", _localizer.Code);
        Assert.Equal("en", vm.Language);
        Assert.Equal("Customize", _localizer["Window.Customize"]);
        Assert.Contains(nameof(HotkeyEditorViewModel.Display), editorChanges);

        vm.Language = "xx";
        Assert.Equal("en", _localizer.Code);

        // Choosing a language is not an edit yet: nothing is saved before Finish.
        Assert.Equal(0, _persistence.SaveCount);
    }

    [Fact]
    public void Next_and_Previous_move_between_the_four_steps()
    {
        using var vm = Create();

        GoToStep(vm, 3);
        Assert.Equal(3, vm.Step);
        Assert.False(vm.NextCommand.CanExecute(null));
        Assert.True(vm.FinishCommand.CanExecute(null));

        vm.Next();
        Assert.Equal(3, vm.Step);

        vm.PreviousCommand.Execute(null);
        Assert.Equal(2, vm.Step);
        Assert.False(vm.FinishCommand.CanExecute(null));

        vm.Previous();
        vm.Previous();
        vm.Previous();
        Assert.Equal(0, vm.Step);
    }

    [Fact]
    public void Wizard_Next_is_blocked_on_a_blocking_hotkey_issue()
    {
        using var vm = Create();
        GoToStep(vm, 1);
        var canExecuteChanged = 0;
        vm.NextCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.Hotkey.StartCapture();
        _hotkeys.Press(WinL);

        Assert.True(canExecuteChanged > 0);
        Assert.False(vm.NextCommand.CanExecute(null));
        vm.Next();
        Assert.Equal(1, vm.Step);

        // Going back stays possible.
        Assert.True(vm.PreviousCommand.CanExecute(null));

        vm.Hotkey.ResetToDefault();
        Assert.True(vm.NextCommand.CanExecute(null));
        vm.Next();
        Assert.Equal(2, vm.Step);
    }

    [Fact]
    public void Leaving_the_hotkey_step_stops_the_capture()
    {
        using var vm = Create();
        GoToStep(vm, 1);
        vm.Hotkey.StartCapture();

        vm.Next();

        Assert.Equal(2, vm.Step);
        Assert.False(vm.Hotkey.IsCapturing);
        Assert.True(_hotkeys.LastHandleDisposed);

        // A combination pressed on a later step (Win+L, say) no longer reaches the editor.
        _hotkeys.Press(WinL);
        _hotkeys.LastCallback!(WinL);
        Assert.Equal(Hotkey.Default, vm.Hotkey.Current);

        GoToStep(vm, WizardViewModel.LastStep);
        Assert.True(vm.FinishCommand.CanExecute(null));
    }

    [Fact]
    public void Going_back_stops_the_capture()
    {
        using var vm = Create();
        GoToStep(vm, 1);
        vm.Hotkey.StartCapture();

        vm.Previous();

        Assert.Equal(0, vm.Step);
        Assert.False(vm.Hotkey.IsCapturing);
        Assert.True(_hotkeys.LastHandleDisposed);
    }

    [Fact]
    public void Finish_is_not_possible_with_a_blocking_hotkey()
    {
        using var vm = Create();
        GoToStep(vm, WizardViewModel.LastStep);
        var finished = 0;
        vm.Finished += (_, _) => finished++;
        var canExecuteChanged = 0;
        vm.FinishCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        // However a refused combination got there, it is never saved.
        vm.Hotkey.StartCapture();
        _hotkeys.Press(WinL);

        Assert.Equal(WinL, vm.Hotkey.Current);
        Assert.True(canExecuteChanged > 0);
        Assert.False(vm.FinishCommand.CanExecute(null));
        vm.Finish();
        Assert.Equal(0, finished);
        Assert.Equal(0, _persistence.SaveCount);
        Assert.Empty(_autostart.Calls);

        vm.Hotkey.ResetToDefault();
        Assert.True(vm.FinishCommand.CanExecute(null));
        vm.Finish();
        Assert.Equal(1, finished);
        Assert.Equal(Hotkey.Default, _persistence.Saved.Single().Settings.Hotkey);
    }

    [Fact]
    public void A_blocking_hotkey_does_not_block_the_other_steps()
    {
        using var vm = Create();
        vm.Hotkey.StartCapture();
        _hotkeys.Press(WinL);

        Assert.True(vm.NextCommand.CanExecute(null));
        vm.Next();
        Assert.Equal(1, vm.Step);
        Assert.False(vm.NextCommand.CanExecute(null));
    }

    [Fact]
    public void Wizard_finish_creates_sample_tab_and_marks_first_run_done()
    {
        using var vm = Create();
        vm.Hotkey.StartCapture();
        _hotkeys.Press(CtrlShiftF);
        vm.Mode = ThemeMode.Dark;
        vm.Theme = "cards";

        var config = FinishAndCapture(vm);

        var tab = Assert.Single(config.Tabs);
        Assert.Equal("Exemple", tab.Name);
        Assert.Equal(tab.Id, config.Settings.ActiveTabId);
        var group = Assert.Single(tab.Columns.SelectMany(c => c.Groups));
        Assert.Equal("Windows", group.Name);
        Assert.Equal(
            [SpecialFolderKey.Desktop, SpecialFolderKey.Documents, SpecialFolderKey.Downloads, SpecialFolderKey.Pictures],
            group.Items.Select(i => i.Special));

        Assert.True(config.Settings.FirstRunCompleted);
        Assert.Equal("fr", config.Settings.Language);
        Assert.Equal(CtrlShiftF, config.Settings.Hotkey);
        Assert.True(config.Settings.StartWithWindows);
        Assert.Equal(ThemeMode.Dark, config.Appearance.Mode);
        Assert.Equal("cards", config.Appearance.Theme);

        Assert.Same(config, Assert.Single(_persistence.Saved));
        Assert.Equal([(true, ExePath)], _autostart.Calls);
        Assert.False(vm.AutostartFailed);
    }

    [Fact]
    public void Wizard_finish_with_an_empty_tab()
    {
        _system.UiCultureName = "en-GB";
        using var vm = Create();
        vm.StartContent = StartContent.EmptyTab;
        vm.StartWithWindows = false;

        var config = FinishAndCapture(vm);

        var tab = Assert.Single(config.Tabs);
        Assert.Equal("My folders", tab.Name);
        Assert.Equal(2, tab.Columns.Count);
        Assert.All(tab.Columns, column => Assert.Empty(column.Groups));
        Assert.Equal("en", config.Settings.Language);
        Assert.Equal(Hotkey.Default, config.Settings.Hotkey);
        Assert.False(config.Settings.StartWithWindows);
        Assert.True(config.Settings.FirstRunCompleted);
        Assert.Equal([(false, ExePath)], _autostart.Calls);
    }

    [Fact]
    public void Wizard_finishes_even_when_autostart_fails()
    {
        _autostart.Throws = new UnauthorizedAccessException();
        using var vm = Create();

        var config = FinishAndCapture(vm);

        Assert.True(vm.AutostartFailed);
        Assert.True(config.Settings.FirstRunCompleted);
        Assert.Same(config, Assert.Single(_persistence.Saved));
    }

    [Fact]
    public void Finish_happens_only_at_the_last_step_and_only_once()
    {
        using var vm = Create();
        var finished = 0;
        vm.Finished += (_, _) => finished++;

        vm.Finish();
        Assert.Equal(0, finished);

        GoToStep(vm, WizardViewModel.LastStep);
        vm.Finish();
        vm.Finish();

        Assert.Equal(1, finished);
        Assert.Equal(1, _persistence.SaveCount);
        Assert.False(vm.FinishCommand.CanExecute(null));
        Assert.False(vm.PreviousCommand.CanExecute(null));
    }

    [Fact]
    public void Finish_ends_a_capture_in_progress()
    {
        using var vm = Create();
        GoToStep(vm, WizardViewModel.LastStep);
        vm.Hotkey.StartCapture();

        vm.Finish();

        Assert.False(vm.Hotkey.IsCapturing);
        Assert.True(_hotkeys.LastHandleDisposed);
    }

    [Fact]
    public void Unknown_theme_and_mode_are_ignored()
    {
        using var vm = Create();

        vm.Theme = "nope";
        vm.Mode = (ThemeMode)42;

        Assert.Equal("fluent", vm.Theme);
        Assert.Equal(ThemeMode.Light, vm.Mode);
    }

    [Fact]
    public void Sample_tab_gets_automatic_icons()
    {
        using var vm = Create();

        var config = FinishAndCapture(vm);

        var tab = Assert.Single(config.Tabs);
        Assert.Equal("🐱", tab.Icon);
        Assert.False(tab.IconChosen);
        var group = Assert.Single(tab.Columns[0].Groups);
        Assert.Equal("🏖", group.Icon);
        Assert.False(group.IconChosen);
    }

    [Fact]
    public void Empty_tab_whose_name_matches_nothing_gets_no_icon()
    {
        using var vm = Create();
        vm.StartContent = StartContent.EmptyTab;

        var config = FinishAndCapture(vm);

        Assert.Null(Assert.Single(config.Tabs).Icon);
    }
}
