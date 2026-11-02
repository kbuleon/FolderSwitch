// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Presentation.Tests.Support;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.Presentation.Tests;

/// <summary>The test zone of the wizard's shortcut step: what is registered, when, and where its trigger goes.</summary>
public class WizardHotkeyTestTests
{
    private const string ExePath = @"C:\Users\Me\AppData\Local\FolderSwitch\current\FolderSwitch.exe";

    private static readonly Hotkey CtrlShiftF = new(KeyModifiers.Ctrl | KeyModifiers.Shift, 'F');
    private static readonly Hotkey CtrlAltF = new(KeyModifiers.Ctrl | KeyModifiers.Alt, 'F');
    private static readonly Hotkey WinL = new(KeyModifiers.Win, VirtualKeys.L);

    private readonly Localizer _localizer = TestLocalizer.Create();
    private readonly FakeSystemInfo _system = new() { UiCultureName = "fr-FR" };
    private readonly FakeHotkeyService _hotkeys = new();
    private readonly FakeAutostart _autostart = new();
    private readonly FakeConfigPersistence _persistence = new();

    private WizardViewModel CreateWizard() => new(_localizer, _system, _hotkeys, _autostart, ExePath, _persistence, TestIcons.Lazy());

    private static void GoToStep(WizardViewModel wizard, int step)
    {
        while (wizard.Step < step)
        {
            wizard.NextCommand.Execute(null);
        }

        while (wizard.Step > step)
        {
            wizard.PreviousCommand.Execute(null);
        }
    }

    private void Capture(WizardViewModel wizard, Hotkey pressed)
    {
        wizard.Hotkey.StartCapture();
        _hotkeys.Press(pressed);
    }

    [Fact]
    public void Nothing_is_registered_before_the_shortcut_step()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);

        Assert.Null(test.Registered);
        Assert.Empty(_hotkeys.RegisterCalls);
        Assert.Null(_hotkeys.Current);
    }

    [Fact]
    public void The_shortcut_step_registers_the_shortcut_shown()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);

        GoToStep(wizard, WizardViewModel.HotkeyStep);

        Assert.Equal(Hotkey.Default, test.Registered);
        Assert.Equal(Hotkey.Default, _hotkeys.Current);
    }

    [Fact]
    public void Started_on_the_shortcut_step_it_registers_at_once()
    {
        using var wizard = CreateWizard();
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        using var test = new WizardHotkeyTest(wizard, _hotkeys);

        Assert.Equal(Hotkey.Default, _hotkeys.Current);
    }

    [Fact]
    public void Leaving_the_step_releases_the_shortcut_and_coming_back_registers_it_again()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        GoToStep(wizard, WizardViewModel.HotkeyStep + 1);
        Assert.Null(test.Registered);
        Assert.Null(_hotkeys.Current);

        GoToStep(wizard, WizardViewModel.HotkeyStep);
        Assert.Equal(Hotkey.Default, _hotkeys.Current);

        GoToStep(wizard, 0);
        Assert.Null(_hotkeys.Current);
        Assert.Equal(2, _hotkeys.UnregisterCount);
    }

    [Fact]
    public void A_new_shortcut_replaces_the_one_registered()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        Capture(wizard, CtrlShiftF);

        Assert.Equal(CtrlShiftF, test.Registered);
        Assert.Equal(CtrlShiftF, _hotkeys.Current);
        Assert.Equal([Hotkey.Default, CtrlShiftF], _hotkeys.RegisterCalls);
        Assert.Equal(1, _hotkeys.UnregisterCount);
    }

    [Fact]
    public void A_shortcut_with_a_warning_is_registered()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        Capture(wizard, CtrlAltF);

        Assert.Contains(HotkeyIssue.CtrlAltLikeAltGr, wizard.Hotkey.Issues);
        Assert.Equal(CtrlAltF, _hotkeys.Current);
    }

    [Fact]
    public void A_refused_shortcut_leaves_nothing_registered_and_the_default_comes_back()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        Capture(wizard, WinL);

        // The previous one is released: its trigger must not say « recognized » for ⊞ Win + L.
        Assert.Null(test.Registered);
        Assert.Null(_hotkeys.Current);
        Assert.DoesNotContain(WinL, _hotkeys.RegisterCalls);

        wizard.Hotkey.ResetToDefault();
        Assert.Equal(Hotkey.Default, _hotkeys.Current);
    }

    [Fact]
    public void A_shortcut_taken_meanwhile_is_reported_and_leaves_nothing_registered()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        var refusals = new List<HotkeyTestRefusal>();
        test.Refused += (_, refusal) => refusals.Add(refusal);
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        // The probe could not tell (it counts as free); the registration says it is taken.
        _hotkeys.ProbeThrows = new InvalidOperationException("probe failed");
        _hotkeys.Taken.Add(CtrlShiftF);
        Capture(wizard, CtrlShiftF);

        Assert.True(wizard.Hotkey.CanApply);
        Assert.Null(test.Registered);
        Assert.Null(_hotkeys.Current);
        var refusal = Assert.Single(refusals);
        Assert.Equal(CtrlShiftF, refusal.Hotkey);
        Assert.Contains(HotkeyIssue.TakenByOtherApp, refusal.Issues);
        Assert.Null(refusal.Error);

        // Tried once for the change, again when the step is shown anew.
        Assert.Single(_hotkeys.RegisterCalls, CtrlShiftF);
        _hotkeys.Taken.Remove(CtrlShiftF);
        GoToStep(wizard, WizardViewModel.HotkeyStep + 1);
        GoToStep(wizard, WizardViewModel.HotkeyStep);
        Assert.Equal(CtrlShiftF, _hotkeys.Current);
    }

    [Fact]
    public void A_registration_that_fails_is_reported_and_leaves_nothing_registered()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        var refusals = new List<HotkeyTestRefusal>();
        test.Refused += (_, refusal) => refusals.Add(refusal);
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        var failure = new Win32Exception(5);
        _hotkeys.RegisterThrows = failure;
        Capture(wizard, CtrlShiftF);

        Assert.Null(test.Registered);
        Assert.Null(_hotkeys.Current);
        Assert.Same(failure, Assert.Single(refusals).Error);
    }

    [Fact]
    public void The_trigger_goes_to_the_test_zone_of_the_shortcut_step()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);

        _hotkeys.Trigger();
        Assert.False(wizard.Hotkey.TestRecognized);

        GoToStep(wizard, WizardViewModel.HotkeyStep);
        _hotkeys.Trigger();
        Assert.True(wizard.Hotkey.TestRecognized);

        // A new shortcut is tested anew.
        Capture(wizard, CtrlShiftF);
        Assert.False(wizard.Hotkey.TestRecognized);
        _hotkeys.Trigger();
        Assert.True(wizard.Hotkey.TestRecognized);
    }

    [Fact]
    public void A_trigger_without_a_registration_says_nothing()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        GoToStep(wizard, WizardViewModel.HotkeyStep);
        Capture(wizard, WinL);

        _hotkeys.Trigger();

        Assert.False(wizard.Hotkey.TestRecognized);
    }

    [Fact]
    public void Finishing_leaves_nothing_registered()
    {
        using var wizard = CreateWizard();
        using var test = new WizardHotkeyTest(wizard, _hotkeys);
        GoToStep(wizard, WizardViewModel.HotkeyStep);
        Capture(wizard, CtrlShiftF);
        GoToStep(wizard, WizardViewModel.LastStep);

        wizard.FinishCommand.Execute(null);

        Assert.Null(test.Registered);
        Assert.Null(_hotkeys.Current);
        Assert.Equal(CtrlShiftF, Assert.Single(_persistence.Saved).Settings.Hotkey);
    }

    [Fact]
    public void Closing_the_wizard_on_the_shortcut_step_releases_the_shortcut_and_stops_listening()
    {
        using var wizard = CreateWizard();
        var test = new WizardHotkeyTest(wizard, _hotkeys);
        GoToStep(wizard, WizardViewModel.HotkeyStep);

        test.Dispose();

        Assert.Null(test.Registered);
        Assert.Null(_hotkeys.Current);
        Assert.Equal(1, _hotkeys.UnregisterCount);
        Assert.Equal(0, _hotkeys.TriggeredListeners);

        // The wizard no longer drives any registration.
        Capture(wizard, CtrlShiftF);
        Assert.Equal([Hotkey.Default], _hotkeys.RegisterCalls);
        test.Dispose();
        Assert.Equal(1, _hotkeys.UnregisterCount);
    }

    [Fact]
    public void Closing_the_wizard_elsewhere_unregisters_nothing()
    {
        using var wizard = CreateWizard();
        var test = new WizardHotkeyTest(wizard, _hotkeys);

        test.Dispose();

        Assert.Equal(0, _hotkeys.UnregisterCount);
    }
}
