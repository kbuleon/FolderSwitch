// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Services;
using FolderSwitch.App.Themes;
using FolderSwitch.App.Views;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Icons;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Presentation.Services;
using FolderSwitch.Presentation.ViewModels;
using FolderSwitch.Windows.Hotkeys;
using LanguageRules = FolderSwitch.Core.Localization.Languages;

namespace FolderSwitch.App.Startup;

/// <summary>
/// The first run (spec 5.8): the wizard's window, shown modally before the shortcut is registered and before the view
/// models and the main window are built (the configuration they start from is the one the wizard creates), and what
/// goes with it while it is open.
/// <list type="bullet">
/// <item>Its look follows the mode and theme chosen at step 3, live (<see cref="WizardTheme"/>); its texts follow the
/// language chosen at step 1.</item>
/// <item>The test zone of step 2 (<see cref="WizardHotkeyTest"/>): while that step is shown and the shortcut has no
/// blocking problem, the shortcut is registered for the time being, and its trigger goes to the zone; it is registered
/// again whenever it changes, and released when the step is left, the wizard finished or closed.</item>
/// <item>Its message boxes (a refused autostart, after « Finish ») are owned by its window.</item>
/// </list>
/// <see cref="Run"/> gives the configuration the wizard created and saved, or <c>null</c> when the window was closed
/// without finishing: then nothing was saved, and the next start shows the wizard again.
/// </summary>
/// <remarks>UI thread only. Runs once.</remarks>
internal sealed class FirstRunWizard : IDisposable
{
    private readonly Localizer _localizer;
    private readonly ISystemInfo _system;
    private readonly IHotkeyService _hotkeys;
    private readonly IForegroundActivator _foreground;
    private readonly ViewTexts _texts;
    private readonly WizardTheme _theme;
    private readonly WizardViewModel _viewModel;
    private readonly UserPrompts _prompts;

    private WizardWindow? _window;
    private AppConfig? _created;
    private bool _ran;
    private bool _disposed;

    /// <param name="icons">Gives the automatic icons of the tab and the group the wizard creates.</param>
    /// <param name="resources">Where the wizard's look goes while it is open: the application's resources.</param>
    public FirstRunWizard(
        Localizer localizer,
        ISystemInfo system,
        IHotkeyService hotkeys,
        IAutostart autostart,
        string exePath,
        IConfigPersistence persistence,
        Lazy<IconFinder> icons,
        IForegroundActivator foreground,
        ViewTexts texts,
        ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(hotkeys);
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(resources);

        _localizer = localizer;
        _system = system;
        _hotkeys = hotkeys;
        _foreground = foreground;
        _texts = texts;

        _theme = new WizardTheme(resources, system);
        try
        {
            _viewModel = new WizardViewModel(localizer, system, hotkeys, autostart, exePath, persistence, icons, KeyNames.Instance);
        }
        catch
        {
            _theme.Dispose();
            throw;
        }

        _prompts = new UserPrompts(localizer, () => _window, silent: false);
        _theme.Apply(_viewModel.Mode, _viewModel.Theme);
    }

    /// <summary>
    /// Shows the wizard until it is finished or closed. Returns the configuration it created, already saved, with the
    /// first run done; <c>null</c> when it was closed without finishing (nothing saved).
    /// </summary>
    public AppConfig? Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ran)
        {
            throw new InvalidOperationException("The first-run wizard runs once.");
        }

        _ran = true;
        var window = new WizardWindow(_viewModel, _texts, WindowsLanguage());
        _window = window;
        var test = new WizardHotkeyTest(_viewModel, _hotkeys);
        test.Refused += OnTestRefused;
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Finished += OnFinished;
        try
        {
            DebugLog.Info($"First run: the wizard is shown (language {_localizer.Code})");
            window.ShowDialog();
        }
        finally
        {
            _viewModel.Finished -= OnFinished;
            _viewModel.PropertyChanged -= OnViewModelChanged;
            test.Refused -= OnTestRefused;

            // No shortcut is left registered for the test, whatever way the wizard ended.
            test.Dispose();
            _window = null;
            window.Release();
        }

        if (_created is { } created)
        {
            DebugLog.Info($"First run completed: language {created.Settings.Language}, shortcut {Describe(created.Settings.Hotkey)}, "
                + $"{created.Appearance.Theme} {created.Appearance.Mode}, start with Windows {(created.Settings.StartWithWindows ? "on" : "off")}"
                + (_viewModel.AutostartFailed ? " (refused by Windows)" : string.Empty));
        }
        else
        {
            DebugLog.Info("First run: the wizard was closed without finishing; nothing was saved");
        }

        return _created;
    }

    /// <summary>A second launch during the first run: the wizard comes to the front.</summary>
    public void BringToFront()
    {
        if (_window is not { IsVisible: true } window)
        {
            return;
        }

        window.Activate();
        _foreground.Activate(new WindowInteropHelper(window).Handle);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.Dispose();
        _theme.Dispose();
    }

    /// <summary>The shortcut for the log, untranslated: <c>Ctrl + Win</c>.</summary>
    private static string Describe(Hotkey hotkey) =>
        HotkeyRules.Format(hotkey, key => key.StartsWith("Key.", StringComparison.Ordinal) ? key[4..] : key);

    /// <summary>The language preselected, when it is the one Windows uses (not English for want of it); else <c>null</c>.</summary>
    private string? WindowsLanguage()
    {
        var windows = _system.UiCultureName?.Trim() ?? string.Empty;
        var initial = LanguageRules.SelectInitial(windows, _localizer.Available.Select(language => language.Code).ToList());
        return string.Equals(Neutral(initial), Neutral(windows), StringComparison.OrdinalIgnoreCase) ? initial : null;

        static string Neutral(string code) => code.Split('-')[0];
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "" or nameof(WizardViewModel.Mode) or nameof(WizardViewModel.Theme))
        {
            _theme.Apply(_viewModel.Mode, _viewModel.Theme);
        }
    }

    private void OnTestRefused(object? sender, HotkeyTestRefusal refusal)
    {
        var why = refusal.Error is null ? $" ({string.Join(", ", refusal.Issues)})" : string.Empty;
        DebugLog.Warn($"First run: the shortcut {Describe(refusal.Hotkey)} could not be registered for its test{why}", refusal.Error);
    }

    /// <summary>
    /// « Finish »: the configuration is saved (by the view model), the test shortcut released (by
    /// <see cref="WizardHotkeyTest"/>). A refused autostart is told, in a box owned by the wizard; then the window closes
    /// and <see cref="Run"/> returns.
    /// </summary>
    private void OnFinished(object? sender, AppConfig config)
    {
        _created = config;

        if (_viewModel.AutostartFailed)
        {
            DebugLog.Warn("First run: FolderSwitch could not be set to start with Windows");
            _prompts.ShowError("Wizard.AutostartFailed");
        }

        _window?.Close();
    }
}
