// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Core.Services;
using FolderSwitch.Presentation.Services;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// The settings window: language, shortcut, start with Windows, export and import of the configuration, update
/// check. Every change takes effect at once and is saved exactly once; a change the system
/// refuses is reported (<see cref="IUserPrompts.ShowError"/>) and leaves the setting as it was.
/// Meant to be used from the UI thread.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly IHotkeyService _hotkeys;
    private readonly IAutostart _autostart;
    private readonly string _exePath;
    private readonly ConfigStore _store;
    private readonly IUserPrompts _prompts;
    private readonly IConfigPersistence _persistence;
    private readonly Func<Task>? _checkUpdates;

    private AppConfig _config;
    private bool _startWithWindows;
    private bool _isUpdateBusy;
    private bool _isHotkeyRegistered = true;

    /// <param name="hotkey">The shortcut editor, starting from the shortcut of <paramref name="config"/>.</param>
    /// <param name="exePath">The executable to start with Windows.</param>
    /// <param name="store">Where backups are made and from which files are exported and imported.</param>
    /// <param name="checkUpdates">Checks for a new version and offers it; <c>null</c> when updates are not available.</param>
    public SettingsViewModel(
        AppConfig config,
        Localizer localizer,
        HotkeyEditorViewModel hotkey,
        IHotkeyService hotkeys,
        IAutostart autostart,
        string exePath,
        ConfigStore store,
        IUserPrompts prompts,
        IConfigPersistence persistence,
        Func<Task>? checkUpdates)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(hotkeys);
        ArgumentNullException.ThrowIfNull(autostart);
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(persistence);

        _config = config;
        _localizer = localizer;
        Hotkey = hotkey;
        _hotkeys = hotkeys;
        _autostart = autostart;
        _exePath = exePath;
        _store = store;
        _prompts = prompts;
        _persistence = persistence;
        _checkUpdates = checkUpdates;

        Languages = LanguageOption.ListFrom(localizer);
        _startWithWindows = autostart.IsEnabled;

        // The configured shortcut is the one in effect: the editor's test zone tests only that one.
        Hotkey.SetInEffect(config.Settings.Hotkey);
        Hotkey.PropertyChanged += OnHotkeyChanged;
    }

    /// <summary>
    /// Raised after an import replaced the configuration, which is already saved and edited from then on: the
    /// application hands it to <c>MainViewModel.Reload</c> and <c>AppearanceViewModel.Reload</c>.
    /// </summary>
    public event EventHandler<AppConfig>? ConfigReplaced;

    /// <summary>The languages that can be chosen.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; }

    /// <summary>
    /// Code of the language shown. Choosing one switches the interface at once and records it in the
    /// configuration; a code no language has is ignored.
    /// </summary>
    public string Language
    {
        get => _localizer.Code;
        set
        {
            if (FindLanguage(value) is not { } language
                || (string.Equals(_config.Settings.Language, language.Code, StringComparison.Ordinal)
                    && string.Equals(_localizer.Code, language.Code, StringComparison.Ordinal)))
            {
                return;
            }

            _localizer.SetLanguage(language.Code);
            _config.Settings.Language = language.Code;
            OnPropertyChanged();
            Save();
        }
    }

    /// <summary>The shortcut editor; <see cref="ApplyHotkey"/> puts its combination into effect.</summary>
    public HotkeyEditorViewModel Hotkey { get; }

    /// <summary>
    /// Whether FolderSwitch starts with Windows, as the system has it now. A change is written to the system first;
    /// when the system refuses it, the error is shown and the value goes back to what it was.
    /// </summary>
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (value == _startWithWindows)
            {
                return;
            }

            try
            {
                _autostart.Set(value, _exePath);
            }
            catch (Exception e) when (IsStorageError(e))
            {
                // The checkbox already shows the new value: tell it to read the old one again.
                OnPropertyChanged();
                _prompts.ShowError("Error.ActionFailed");
                return;
            }

            _startWithWindows = value;
            _config.Settings.StartWithWindows = value;
            OnPropertyChanged();
            Save();
        }
    }

    /// <summary>
    /// Whether the shortcut of the configuration is registered, as the application found at startup (true until it
    /// says otherwise); an « Apply » or an import that registers one makes it true again.
    /// </summary>
    public bool IsHotkeyRegistered
    {
        get => _isHotkeyRegistered;
        set
        {
            if (SetProperty(ref _isHotkeyRegistered, value))
            {
                // The editor tests, and shows as in effect, only a shortcut that can be pressed.
                Hotkey.SetRegistered(value);
            }
        }
    }

    /// <summary>
    /// The combination shown is the shortcut in effect: there is nothing to apply (<see cref="ApplyHotkeyCommand"/> is
    /// disabled), and the settings say the shortcut is active.
    /// </summary>
    public bool IsHotkeyApplied => Hotkey.IsInEffect;

    /// <summary>
    /// Registers the editor's combination in place of the current shortcut, then records it. Only possible without a
    /// blocking problem, and when the combination is not the shortcut in effect already. When the registration is refused (taken in the meantime, say), the reason is shown, the
    /// editor checks the combination again so that it shows the problem too, and the previous shortcut stays in
    /// effect and in the configuration.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyHotkey))]
    public void ApplyHotkey()
    {
        if (!CanApplyHotkey())
        {
            return;
        }

        var hotkey = Hotkey.Current;
        if (Register(hotkey) is { } errorKey)
        {
            Hotkey.Revalidate();
            _prompts.ShowError(errorKey);
            return;
        }

        Hotkey.SetInEffect(hotkey);
        IsHotkeyRegistered = true;
        if (hotkey == _config.Settings.Hotkey)
        {
            return;
        }

        _config.Settings.Hotkey = hotkey;
        Save();
    }

    /// <summary>Writes the configuration to a file chosen by the user; nothing when the choice is cancelled.</summary>
    [RelayCommand]
    public void Export()
    {
        var path = _prompts.PickExportPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            _store.Export(_config, path);
        }
        catch (Exception e) when (IsStorageError(e))
        {
            _prompts.ShowError("Error.ActionFailed");
        }
    }

    /// <summary>
    /// Replaces the configuration by a file chosen by the user. The file is read and checked first: one that cannot be
    /// read or is not a configuration replaces nothing and makes no backup (which would push the oldest one out).
    /// Then the current configuration is copied among the backups (a failure there replaces nothing either), and
    /// only then replaced. The imported configuration
    /// brings its language and shortcut, which take effect at once (a shortcut refused here leaves the current one,
    /// with the reason shown); whether FolderSwitch starts with Windows, and whether its wizard was completed, stay as
    /// they are on this PC. It is saved, edited from then on, and announced by <see cref="ConfigReplaced"/>.
    /// </summary>
    [RelayCommand]
    public void Import()
    {
        var path = _prompts.PickImportPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AppConfig imported;
        try
        {
            imported = _store.Import(path);

            // config.json holds the configuration being replaced: every change is saved at once.
            _store.BackupOnStartup();
        }
        catch (Exception e) when (e is ConfigFormatException || IsStorageError(e))
        {
            _prompts.ShowError("Error.ActionFailed");
            return;
        }

        var previous = _config;
        imported.Settings.StartWithWindows = _startWithWindows;
        imported.Settings.FirstRunCompleted = previous.Settings.FirstRunCompleted;

        if (FindLanguage(imported.Settings.Language) is { } language)
        {
            _localizer.SetLanguage(language.Code);
        }

        string? hotkeyError = null;
        if (imported.Settings.Hotkey != previous.Settings.Hotkey)
        {
            hotkeyError = Register(imported.Settings.Hotkey);
            if (hotkeyError is not null)
            {
                imported.Settings.Hotkey = previous.Settings.Hotkey;
            }
            else
            {
                IsHotkeyRegistered = true;
            }
        }

        _config = imported;
        Hotkey.SetInEffect(imported.Settings.Hotkey);
        Hotkey.Show(imported.Settings.Hotkey);
        OnPropertyChanged(nameof(Language));
        Save();
        ConfigReplaced?.Invoke(this, imported);

        if (hotkeyError is not null)
        {
            _prompts.ShowError(hotkeyError);
        }
    }

    /// <summary>
    /// Whether an update check or download is running (started by the timer or by the notification menu, which the
    /// application tells): <see cref="CheckUpdatesCommand"/> is disabled until it ends.
    /// </summary>
    public bool IsUpdateBusy
    {
        get => _isUpdateBusy;
        set
        {
            if (SetProperty(ref _isUpdateBusy, value))
            {
                CheckUpdatesCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Checks for updates; disabled when no update check was given, and while one is running (<see cref="IsUpdateBusy"/>,
    /// and the command's own run). Its failures are its own to report.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    public Task CheckUpdatesAsync() => _checkUpdates?.Invoke() ?? Task.CompletedTask;

    /// <summary>How the file system and the registry refuse an operation.</summary>
    private static bool IsStorageError(Exception e) => e is IOException or UnauthorizedAccessException or SecurityException;

    private bool CanApplyHotkey() => Hotkey.CanApply && !IsHotkeyApplied;

    private bool CanCheckUpdates() => _checkUpdates is not null && !_isUpdateBusy;

    /// <summary>Registers <paramref name="h"/>; <c>null</c> when it is in effect, else the key of the message explaining why not.</summary>
    private string? Register(Hotkey h)
    {
        IReadOnlyList<HotkeyIssue> issues;
        try
        {
            issues = _hotkeys.Register(h);
        }
        catch (Exception e) when (HotkeyEditorViewModel.IsServiceFailure(e))
        {
            // The keyboard hook could not be installed, or the service is shutting down.
            return "Error.ActionFailed";
        }

        foreach (var issue in issues)
        {
            if (HotkeyRules.IsBlocking(issue))
            {
                return "Hotkey.Issue." + issue;
            }
        }

        return null;
    }

    private LanguageOption? FindLanguage(string? code) => LanguageOption.Find(Languages, code);

    private void OnHotkeyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(HotkeyEditorViewModel.CanApply))
        {
            ApplyHotkeyCommand.NotifyCanExecuteChanged();
        }

        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(HotkeyEditorViewModel.IsInEffect))
        {
            OnHotkeyAppliedChanged();
        }
    }

    private void OnHotkeyAppliedChanged()
    {
        OnPropertyChanged(nameof(IsHotkeyApplied));
        ApplyHotkeyCommand.NotifyCanExecuteChanged();
    }

    private void Save() => _persistence.Save(_config);
}
