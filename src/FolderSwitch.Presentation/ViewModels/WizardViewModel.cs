using System.ComponentModel;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.Services;
using LanguageRules = FolderSwitch.Core.Localization.Languages;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// The first-run wizard, in four steps: 0 language, 1 shortcut, 2 appearance, 3 startup. Nothing is written before
/// <see cref="Finish"/>, except the language, which the interface follows as soon as it is chosen. Meant to be used
/// from the UI thread.
/// </summary>
public sealed partial class WizardViewModel : ObservableObject, IDisposable
{
    /// <summary>The startup step, where <see cref="Finish"/> is offered.</summary>
    public const int LastStep = 3;

    /// <summary>The shortcut step, where <see cref="Hotkey"/> is shown (and its test zone).</summary>
    public const int HotkeyStep = 1;

    private const string DefaultTheme = "fluent";

    private readonly Localizer _localizer;
    private readonly IAutostart _autostart;
    private readonly string _exePath;
    private readonly IConfigPersistence _persistence;

    private ThemeMode _mode = ThemeMode.Light;
    private string _theme = DefaultTheme;
    private StartContent _startContent = StartContent.SampleTab;
    private bool _finished;
    private bool _disposed;

    /// <param name="system">Gives the Windows display language, from which the language is preselected.</param>
    /// <param name="hotkeys">Used by the shortcut editor for the capture and to probe combinations.</param>
    /// <param name="exePath">The executable to start with Windows.</param>
    /// <param name="keyNames">The names the keyboard layout gives keys, for the shortcut editor (<c>null</c>: standard ones).</param>
    public WizardViewModel(
        Localizer localizer,
        ISystemInfo system,
        IHotkeyService hotkeys,
        IAutostart autostart,
        string exePath,
        IConfigPersistence persistence,
        IKeyNames? keyNames = null)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(hotkeys);
        ArgumentNullException.ThrowIfNull(autostart);
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        ArgumentNullException.ThrowIfNull(persistence);

        _localizer = localizer;
        _autostart = autostart;
        _exePath = exePath;
        _persistence = persistence;

        Languages = LanguageOption.ListFrom(localizer);
        var initial = LanguageRules.SelectInitial(system.UiCultureName, Languages.Select(language => language.Code).ToList());
        if (FindLanguage(initial) is { } preselected)
        {
            _localizer.SetLanguage(preselected.Code);
        }

        StartWithWindows = true;
        Hotkey = new HotkeyEditorViewModel(FolderSwitch.Core.Model.Hotkey.Default, hotkeys, localizer, keyNames);
        Hotkey.PropertyChanged += OnHotkeyChanged;
    }

    /// <summary>Raised once, by <see cref="Finish"/>, with the new configuration, already saved.</summary>
    public event EventHandler<AppConfig>? Finished;

    /// <summary>The current step, 0 to <see cref="LastStep"/>.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(PreviousCommand), nameof(FinishCommand))]
    public partial int Step { get; private set; }

    // ----- Step 0: language -----

    /// <summary>The languages that can be chosen.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; }

    /// <summary>
    /// Code of the language shown: preselected from the Windows display language (English when it is not
    /// available). Choosing one switches the interface at once; a code no language has is ignored.
    /// </summary>
    public string Language
    {
        get => _localizer.Code;
        set
        {
            if (FindLanguage(value) is not { } language || string.Equals(language.Code, _localizer.Code, StringComparison.Ordinal))
            {
                return;
            }

            _localizer.SetLanguage(language.Code);
            OnPropertyChanged();
        }
    }

    // ----- Step 1: shortcut -----

    /// <summary>The shortcut editor, starting at Ctrl + Win. A blocking problem keeps the wizard on this step.</summary>
    public HotkeyEditorViewModel Hotkey { get; }

    // ----- Step 2: appearance -----

    /// <summary>Light (the default), Dark or Auto.</summary>
    public ThemeMode Mode
    {
        get => _mode;
        set
        {
            if (Enum.IsDefined(value))
            {
                SetProperty(ref _mode, value);
            }
        }
    }

    /// <summary>Key of the theme (<c>fluent</c> by default); a key the catalog does not know is ignored.</summary>
    public string Theme
    {
        get => _theme;
        set
        {
            if (value is not null && ThemeCatalog.ThemeKeys.Contains(value))
            {
                SetProperty(ref _theme, value);
            }
        }
    }

    // ----- Step 3: startup -----

    /// <summary>Start FolderSwitch with Windows: recommended and checked by default.</summary>
    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    /// <summary>What the first tab holds: the sample group (the default) or nothing.</summary>
    public StartContent StartContent
    {
        get => _startContent;
        set
        {
            if (Enum.IsDefined(value))
            {
                SetProperty(ref _startContent, value);
            }
        }
    }

    /// <summary>True when <see cref="Finish"/> could not set FolderSwitch to start with Windows.</summary>
    [ObservableProperty]
    public partial bool AutostartFailed { get; private set; }

    /// <summary>
    /// Goes to the next step. Ends a shortcut capture still running: a combination pressed on a later step (where
    /// the editor is not shown) must not become the shortcut.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGoNext))]
    public void Next()
    {
        if (CanGoNext())
        {
            Hotkey.StopCapture();
            Step++;
        }
    }

    /// <summary>Goes back one step, ending a shortcut capture still running.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    public void Previous()
    {
        if (CanGoBack())
        {
            Hotkey.StopCapture();
            Step--;
        }
    }

    /// <summary>
    /// At the last step, once, and only with a shortcut without blocking problem: creates the configuration (a tab
    /// named in the current language, with the sample group or empty) with every choice of the wizard and the first
    /// run marked as done, saves it, then sets FolderSwitch to start with Windows or not. A refused autostart does
    /// not stop the wizard: it is reported by <see cref="AutostartFailed"/>. Raises <see cref="Finished"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanFinish))]
    public void Finish()
    {
        if (!CanFinish())
        {
            return;
        }

        _finished = true;
        Hotkey.StopCapture();

        var sample = StartContent == StartContent.SampleTab;
        var config = DefaultConfigs.Create(
            StartContent,
            _localizer[sample ? "Default.SampleTab" : "Default.EmptyTab"],
            _localizer["Default.SampleGroup"]);
        config.Settings.Language = Language;
        config.Settings.Hotkey = Hotkey.Current;
        config.Settings.StartWithWindows = StartWithWindows;
        config.Settings.FirstRunCompleted = true;
        config.Appearance.Mode = Mode;
        config.Appearance.Theme = Theme;
        _persistence.Save(config);

        try
        {
            _autostart.Set(StartWithWindows, _exePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            AutostartFailed = true;
        }

        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
        Finished?.Invoke(this, config);
    }

    /// <summary>Ends any shortcut capture and releases the editor.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Hotkey.PropertyChanged -= OnHotkeyChanged;
        Hotkey.Dispose();
    }

    private bool CanGoNext() => !_finished && Step < LastStep && (Step != HotkeyStep || Hotkey.CanApply);

    private bool CanGoBack() => !_finished && Step > 0;

    /// <summary>
    /// A refused shortcut is never saved: the resident application would start with no way to summon it. The
    /// wizard already stays on step 1 while it is refused; this is the last guard, whatever way it got there.
    /// </summary>
    private bool CanFinish() => !_finished && Step == LastStep && Hotkey.CanApply;

    private LanguageOption? FindLanguage(string? code) => LanguageOption.Find(Languages, code);

    private void OnHotkeyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(HotkeyEditorViewModel.CanApply))
        {
            NextCommand.NotifyCanExecuteChanged();
            FinishCommand.NotifyCanExecuteChanged();
        }
    }
}
