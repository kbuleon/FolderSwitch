// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Windows;
using System.Windows.Threading;
using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Services;
using FolderSwitch.App.Themes;
using FolderSwitch.App.Tray;
using FolderSwitch.App.Views;
using FolderSwitch.Core.Activation;
using FolderSwitch.Core.Hotkeys;
using FolderSwitch.Core.Localization;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Persistence;
using FolderSwitch.Core.Web;
using FolderSwitch.Presentation.ViewModels;
using FolderSwitch.Windows.Browsers;
using FolderSwitch.Windows.Dialogs;
using FolderSwitch.Windows.Hotkeys;
using FolderSwitch.Windows.Shell;
using FolderSwitch.Windows.SystemServices;
using LanguageRules = FolderSwitch.Core.Localization.Languages;

namespace FolderSwitch.App.Startup;

/// <summary>
/// Builds the resident application, in order: system information, localizer, configuration, services, first run,
/// shortcut, view models, the theme's resources (<see cref="ThemeApplier"/>), the main window (once), the notification
/// icon, then the wiring of the user's triggers.
/// Owns all of it, and releases it in <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>First run</b>: while the configuration says it is not done, the first-run wizard (<see cref="FirstRunWizard"/>)
/// is shown, modally, before the shortcut is registered and before the view models and the main window are built, so
/// that the wizard alone decides the configuration they start from; the composition goes on with the configuration it
/// created. Closed without finishing, it leaves nothing saved, and <see cref="Build"/> returns <c>false</c>: FolderSwitch
/// quits. Once the application is ready, after an installation, a repair or an update
/// (<see cref="StartupOptions.Setup"/>), <see cref="ShowSetupNotice"/> says so in a box, with the shortcut that opens it;
/// after a first run without one, <see cref="AnnounceReady"/> says, in a notification, which shortcut opens it.
/// A self-test leaves the first run to a real start.
/// </para>
/// <para>
/// UI thread only. Apart from the wizard, <see cref="Build"/> shows nothing: what the user must be told about the
/// startup (a configuration restored or reset, a shortcut that could not be registered) is collected in
/// <see cref="StartupNotices"/>, shown once the application is up.
/// </para>
/// </remarks>
internal sealed class Composition : IDisposable
{
    private readonly StartupOptions _options;
    private readonly Action _quit;
    private readonly List<(string Key, bool IsError)> _notices = [];

    private SystemInfo? _system;
    private HotkeyService? _hotkeys;
    private CallContextProvider? _contexts;
    private ExplorerController? _explorer;
    private FileDialogNavigator? _fileDialog;
    private BrowserAddressReader? _browserReader;
    private SocketsHttpHandler? _faviconHandler;
    private FileTypeIcons? _fileTypeIcons;
    private MainViewModel? _main;
    private AppearanceViewModel? _appearance;
    private HotkeyEditorViewModel? _hotkeyEditor;
    private SettingsViewModel? _settings;
    private MainWindow? _window;
    private WindowController? _controller;
    private ThemeApplier? _themeApplier;
    private TrayIcon? _tray;
    private UpdateChecker? _updates;
    private FirstRunWizard? _wizard;
    private Hotkey? _firstRunHotkey;
    private SetupNotice? _setup;
    private Hotkey? _setupHotkey;
    private bool _triggersWired;
    private bool _disposed;

    /// <param name="quit">Ends the application (the notification menu's « Quit »).</param>
    public Composition(StartupOptions options, Action quit)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(quit);

        _options = options;
        _quit = quit;
    }

    /// <summary>The localizer, as soon as it is loaded (also for a startup that failed later).</summary>
    public Localizer? Localizer { get; private set; }

    /// <summary>The prompts, once built.</summary>
    public UserPrompts? Prompts { get; private set; }

    /// <summary>The window controller, once built.</summary>
    public WindowController Controller => _controller ?? throw new InvalidOperationException("Not built yet.");

    /// <summary>The main view model (for the views of later steps).</summary>
    public MainViewModel Main => _main ?? throw new InvalidOperationException("Not built yet.");

    /// <summary>The appearance's view model (for the views of later steps).</summary>
    public AppearanceViewModel Appearance => _appearance ?? throw new InvalidOperationException("Not built yet.");

    /// <summary>The settings window's view model (for the views of later steps).</summary>
    public SettingsViewModel Settings => _settings ?? throw new InvalidOperationException("Not built yet.");

    /// <summary>Messages for the user about the startup, as localization keys, in the order they arose.</summary>
    public IReadOnlyList<(string Key, bool IsError)> StartupNotices => _notices;

    /// <summary>
    /// Builds everything, after the first-run wizard when the first run is not done; does not show the main window
    /// (see <see cref="WindowController.Prerender"/>).
    /// </summary>
    /// <returns><c>false</c> when the first-run wizard was closed without finishing: nothing is built, FolderSwitch quits.</returns>
    /// <exception cref="Exception">A part that FolderSwitch cannot run without failed; <see cref="Dispose"/> releases the rest.</exception>
    public bool Build()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var ui = _options.UiContext;

        _system = new SystemInfo(ui);
        Localizer = LoadLocalizer(_system);

        var (store, config, readOnly) = LoadConfiguration(Localizer);
        ApplyConfiguredLanguage(Localizer, config);

        Prompts = new UserPrompts(Localizer, () => _window, silent: _options.SelfTest);
        var prompts = Prompts;
        var persistence = new ConfigPersistence(store, readOnly, () => prompts.ShowErrorLater("Config.SaveFailed"));
        var exePath = ExecutablePath();
        var autostart = new Autostart();
        var foreground = new ForegroundActivator();
        var texts = new ViewTexts(Localizer);

        _hotkeys = new HotkeyService(ui);
        _contexts = new CallContextProvider();
        _explorer = new ExplorerController();
        _fileDialog = new FileDialogNavigator();
        _browserReader = new BrowserAddressReader();
        _fileTypeIcons = new FileTypeIcons();
        var favicons = CreateFavicons();

        // One source for the Windows generation: the one that decides on tabs is the one the system reports.
        var shell = new ShellLauncher();
        var activation = new ActivationService(
            _explorer, _fileDialog, shell, new PathResolver(), _system.Generation, LogActivationProblem);

        var wizardCompleted = false;
        if (!config.Settings.FirstRunCompleted)
        {
            if (_options.SelfTest)
            {
                DebugLog.Info("Self-test: the first run is left for a real start");
            }
            else
            {
                var created = RunFirstRunWizard(persistence, autostart, exePath, foreground, texts);

                // Closed without finishing (nothing saved), or released meanwhile (the Windows session ending).
                if (created is null || _disposed)
                {
                    return false;
                }

                config = created;
                wizardCompleted = true;
            }
        }

        var hotkeyRegistered = RegisterHotkey(config.Settings.Hotkey, reportFailure: true);
        if (_options.Setup is { } setup)
        {
            // Told once the application is ready (ShowSetupNotice), with the shortcut when it works, after the wizard too:
            // its box says what the ready notification would.
            _setup = setup;
            _setupHotkey = hotkeyRegistered ? config.Settings.Hotkey : null;
        }
        else if (hotkeyRegistered && wizardCompleted)
        {
            // Told once the application is ready (AnnounceReady): the wizard just ended, and nothing else shows.
            _firstRunHotkey = config.Settings.Hotkey;
        }

        _controller = new WindowController(_contexts, foreground, prompts);
        _main = new MainViewModel(config, new FocusReturningActivator(activation, _controller), persistence, prompts, _controller, _browserReader, favicons, Localizer);
        _appearance = new AppearanceViewModel(config, _system, persistence);

        // Before the window: its views read the theme's resources from the first layout on.
        _themeApplier = new ThemeApplier(_appearance, Application.Current.Resources);

        // None (a null delegate: the settings button stays disabled) without a feed address, in a copy that was not
        // installed, and in a self-test.
        _updates = UpdateChecker.TryCreate(UpdateFeed.Url, _options.SelfTest, prompts, _quit);

        _hotkeyEditor = new HotkeyEditorViewModel(config.Settings.Hotkey, _hotkeys, Localizer, KeyNames.Instance);
        _settings = new SettingsViewModel(
            config, Localizer, _hotkeyEditor, _hotkeys, autostart, exePath, store, prompts, persistence,
            checkUpdates: _updates is null ? null : _updates.CheckNowAsync)
        {
            // A shortcut refused at startup can be applied again from the settings.
            IsHotkeyRegistered = hotkeyRegistered,
        };
        _settings.ConfigReplaced += OnConfigReplaced;
        if (_updates is not null)
        {
            _updates.BusyChanged += OnUpdateBusyChanged;
        }

        // Built once, for the whole life of the process.
        _window = new MainWindow(_main, texts, _appearance, _settings);
        _window.View.Icons = new ItemIcons(_fileTypeIcons);
        _window.View.Shell = shell;
        _controller.Attach(_window, _main);

        // A self-test builds the icon but never puts it in the notification area.
        _tray = new TrayIcon(Localizer, show: !_options.SelfTest);

        if (_options.SelfTest)
        {
            // The keyboard hook of the shortcut is installed (the registration is part of what is tested), but a
            // press reaches nothing.
            DebugLog.Info($"Self-test: notification icon built, not shown (created: {_tray.IsCreated}); "
                + "the shortcut, the icon and second launches are not wired to the window");
            return true;
        }

        // Spec 5.1 and 5.9: the shortcut shows, hides or recalls (ShortcutRouting); the icon and a second launch only
        // ever open.
        _hotkeys.Triggered += OnHotkeyTriggered;
        _options.Instance.ActivationRequested += OnOpenRequested;
        _tray.OpenRequested += OnOpenRequested;
        _tray.CustomizeRequested += OnCustomizeRequested;
        _tray.QuitRequested += OnQuitRequested;
        if (_updates is not null)
        {
            _updates.UpdateAvailabilityChanged += OnUpdateAvailabilityChanged;
            _updates.DownloadingChanged += OnUpdateDownloadingChanged;
            _tray.InstallUpdateRequested += OnInstallUpdateRequested;
            _updates.Start();
        }

        _triggersWired = true;
        return true;
    }

    /// <summary>
    /// The application is up after a first run that just ended, without an installation (the configuration was removed,
    /// a development run): a notification says that FolderSwitch is ready and which shortcut opens it, since nothing
    /// else appears once the wizard closes. Does nothing after any other start, after an installation (a box says it,
    /// see <see cref="ShowSetupNotice"/>), or when the shortcut could not be registered (a startup notice says so).
    /// </summary>
    public void AnnounceReady()
    {
        if (_firstRunHotkey is not { } hotkey || _tray is null || Localizer is null)
        {
            return;
        }

        _firstRunHotkey = null;
        _tray.ShowReady(Localizer.Format("Tray.Ready", FormatHotkey(Localizer, hotkey)));
        DebugLog.Info("First run: the ready notification is shown");
    }

    /// <summary>
    /// The application is up after an installation (a first one, a repair) or an update, which end without a word: a box
    /// says so, with the version, and which shortcut opens FolderSwitch when it works, after the wizard of a first
    /// installation too. Once, before the startup notices; does nothing after any other start.
    /// </summary>
    public void ShowSetupNotice()
    {
        if (_setup is not { } setup || Prompts is null || Localizer is null)
        {
            return;
        }

        _setup = null;
        var shortcut = _setupHotkey is { } hotkey ? FormatHotkey(Localizer, hotkey) : null;
        DebugLog.Info($"{setup.Kind} {setup.Version}: the box is shown");
        Prompts.ShowInfo($"Setup.{setup.Kind}", setup.Text(Localizer, shortcut));
    }

    private static string FormatHotkey(Localizer localizer, Hotkey hotkey) =>
        HotkeyRules.Format(hotkey, key => localizer[key], KeyNames.Instance);

    /// <summary>
    /// Releases everything, the sources of input first (shortcut and keyboard hook, single instance, notification
    /// icon) so that nothing fires during the teardown, then the window, then the background workers. Idempotent;
    /// each step is independent of the others' failures.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Release("triggers", UnwireTriggers);
        Release("updates", () =>
        {
            if (_updates is not null)
            {
                _updates.BusyChanged -= OnUpdateBusyChanged;
                _updates.Dispose();
            }
        });
        Release("shortcut", () => _hotkeys?.Dispose());
        Release("single instance", _options.Instance.Dispose);
        Release("notification icon", () => _tray?.Close());
        Release("main window", () =>
        {
            _controller?.Detach();
            _window?.CloseForExit();
        });
        Release("view models", () =>
        {
            if (_settings is not null)
            {
                _settings.ConfigReplaced -= OnConfigReplaced;
            }

            _hotkeyEditor?.Dispose();
            _themeApplier?.Dispose();
            _appearance?.Dispose();
        });
        Release("system information", () => _system?.Dispose());
        Release("call context", () => _contexts?.Dispose());
        Release("Explorer control", () => _explorer?.Dispose());
        Release("file dialogs", () => _fileDialog?.Dispose());
        Release("browser address reader", () => _browserReader?.Dispose());
        Release("file type icons", () => _fileTypeIcons?.Dispose());
        Release("favicon connections", () => _faviconHandler?.Dispose());
        DebugLog.Info("Released");
    }

    /// <summary>
    /// The favicons of the web sites: kept in the `favicons` folder beside the configuration (a throwaway folder in a
    /// self-test), fetched over a handler that keeps no cookie and follows no redirect itself (the fetcher does, and
    /// checks each hop).
    /// </summary>
    private Favicons CreateFavicons()
    {
        _faviconHandler = new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false };
        var fetcher = new FaviconFetcher(_faviconHandler, "FolderSwitch/" + ProductVersion());
        return new Favicons(new FaviconCache(Path.Combine(_options.ConfigDirectory, "favicons")), fetcher, DebugLog.Info);
    }

    /// <summary>The version of the assembly (the build metadata after a <c>+</c> left out), as the settings show it.</summary>
    private static string ProductVersion()
    {
        var assembly = typeof(Composition).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational?.Split('+')[0];
        return string.IsNullOrWhiteSpace(version) ? assembly.GetName().Version?.ToString(3) ?? "0" : version;
    }

    private static bool IsStorageError(Exception e) => e is IOException or UnauthorizedAccessException or SecurityException;

    private static Localizer LoadLocalizer(SystemInfo system)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Strings");
        var localizer = Localizer.LoadFromDirectory(directory);
        if (localizer.Available.Count == 0)
        {
            DebugLog.Error($"No language could be loaded from {directory}: every text shows as its key");
            return localizer;
        }

        // Until the configuration is read: the language of Windows, which also names a new configuration's tab.
        var initial = LanguageRules.SelectInitial(system.UiCultureName, localizer.Available.Select(language => language.Code).ToList());
        SetLanguageIfAvailable(localizer, initial);
        return localizer;
    }

    /// <summary>The configured language when there is one and it can be loaded; otherwise the language of Windows stays.</summary>
    private static void ApplyConfiguredLanguage(Localizer localizer, AppConfig config)
    {
        if (config.Settings.Language is { } configured && !SetLanguageIfAvailable(localizer, configured))
        {
            DebugLog.Warn($"The configured language '{configured}' is not available");
        }

        DebugLog.Info($"Language: {localizer.Code}");
    }

    /// <summary>Only a code of <see cref="Localizer.Available"/> (any other makes <see cref="Localizer.SetLanguage"/> throw).</summary>
    private static bool SetLanguageIfAvailable(Localizer localizer, string code)
    {
        var match = localizer.Available
            .Select(language => language.Code)
            .FirstOrDefault(available => available.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        localizer.SetLanguage(match);
        return true;
    }

    /// <summary>
    /// What a configuration starts from before the first run is completed: one empty tab named in the current language.
    /// </summary>
    private static AppConfig NewConfiguration(Localizer localizer)
    {
        var config = DefaultConfigs.Create(StartContent.EmptyTab, localizer["Default.EmptyTab"], localizer["Default.SampleGroup"]);
        config.Settings.FirstRunCompleted = false;
        return config;
    }

    /// <summary>The executable to start with Windows: the one running (Velopack runs it from a stable folder).</summary>
    private static string ExecutablePath() =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".exe");

    /// <summary>
    /// Loads <c>config.json</c> from <see cref="StartupOptions.ConfigDirectory"/> (a damaged one is replaced by the latest
    /// valid backup, or by a new configuration), then makes the startup backup of the file just read. A file that
    /// cannot be read at all (locked, access denied) is never overwritten: the session runs on a temporary configuration
    /// and saves nothing.
    /// </summary>
    /// <exception cref="Exception">The configuration folder cannot be created.</exception>
    private (ConfigStore Store, AppConfig Config, bool ReadOnly) LoadConfiguration(Localizer localizer)
    {
        var store = new ConfigStore(_options.ConfigDirectory, TimeProvider.System);

        LoadResult result;
        try
        {
            result = store.Load(() => NewConfiguration(localizer));
        }
        catch (Exception e) when (IsStorageError(e))
        {
            DebugLog.Error("The configuration could not be read: a temporary configuration is used, nothing will be saved", e);
            _notices.Add(("Config.LoadFailed", true));
            var temporary = NewConfiguration(localizer);

            // Not a first run: a configuration exists, it just could not be read.
            temporary.Settings.FirstRunCompleted = true;
            return (store, temporary, true);
        }

        DebugLog.Info($"Configuration: {result.Outcome}"
            + (_options.SelfTest ? $" (self-test, throwaway folder {_options.ConfigDirectory})" : string.Empty));
        switch (result.Outcome)
        {
            case LoadOutcome.RestoredFromBackup:
                _notices.Add(("Config.RestoredFromBackup", false));
                break;
            case LoadOutcome.ResetToDefault:
                _notices.Add(("Config.ResetToDefault", false));
                break;
        }

        try
        {
            // After the load: the file is known to be valid (or was just replaced by a valid one).
            store.BackupOnStartup();
        }
        catch (Exception e) when (IsStorageError(e))
        {
            DebugLog.Warn("The startup backup could not be made", e);
        }

        return (store, result.Config, false);
    }

    /// <summary>
    /// The first run: shows the wizard until it is finished or closed (a second launch meanwhile brings it to the
    /// front). Returns the configuration it created, already saved, or <c>null</c> when it was closed without finishing
    /// (nothing saved). Its look, the shortcut of its test zone and its own handlers are gone when this returns.
    /// </summary>
    private AppConfig? RunFirstRunWizard(ConfigPersistence persistence, Autostart autostart, string exePath, ForegroundActivator foreground, ViewTexts texts)
    {
        using var wizard = new FirstRunWizard(
            Localizer!, _system!, _hotkeys!, autostart, exePath, persistence, foreground, texts, Application.Current.Resources);
        _wizard = wizard;
        _options.Instance.ActivationRequested += OnWizardOpenRequested;
        try
        {
            return wizard.Run();
        }
        finally
        {
            _options.Instance.ActivationRequested -= OnWizardOpenRequested;
            _wizard = null;
        }
    }

    private void OnWizardOpenRequested(object? sender, EventArgs e) => _wizard?.BringToFront();

    /// <summary>
    /// Registers the shortcut; a refusal is logged and, if <paramref name="reportFailure"/>, told to the user. Returns
    /// whether it is in effect.
    /// </summary>
    private bool RegisterHotkey(Hotkey hotkey, bool reportFailure)
    {
        try
        {
            var issues = _hotkeys!.Register(hotkey);
            if (issues.Any(HotkeyRules.IsBlocking))
            {
                DebugLog.Warn($"The shortcut {Describe(hotkey)} was refused: {string.Join(", ", issues)}");
                if (reportFailure)
                {
                    _notices.Add(("Startup.HotkeyFailed", true));
                }

                return false;
            }

            DebugLog.Info($"Shortcut registered: {Describe(hotkey)}" + (issues.Count > 0 ? $" (warnings: {string.Join(", ", issues)})" : string.Empty));
            return true;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or ObjectDisposedException)
        {
            DebugLog.Error($"The shortcut {Describe(hotkey)} could not be registered", e);
            if (reportFailure)
            {
                _notices.Add(("Startup.HotkeyFailed", true));
            }

            return false;
        }
    }

    /// <summary>
    /// What the activation service reports of an activation that did not go as planned: how it ended, the plan, the
    /// target and the exception, so that « The action could not be completed » can be explained from the log.
    /// </summary>
    private static void LogActivationProblem(ActivationProblem problem)
    {
        var plan = problem.Plan is { } p
            ? $"{p.Kind}{(p.Fallback is { } fallback ? $" (fallback {fallback})" : string.Empty)}"
            : "none";
        var message = $"Activation: {problem.Outcome}, {problem.Reason}; plan {plan}; target « {problem.Target ?? "?"} »";
        if (problem.Outcome is ActivationOutcome.Done or ActivationOutcome.OpenedElsewhere)
        {
            DebugLog.Info(message + (problem.Error is { } error ? $" ({error.GetType().Name}: {error.Message})" : string.Empty));
        }
        else
        {
            DebugLog.Warn(message, problem.Error);
        }
    }

    /// <summary>The shortcut for the log, untranslated: <c>Ctrl + Win</c>.</summary>
    private static string Describe(Hotkey hotkey) => HotkeyRules.Format(hotkey, key => key.StartsWith("Key.", StringComparison.Ordinal) ? key[4..] : key);

    /// <summary>An import replaced the configuration: every view model edits the new one, and its shortcut is in effect.</summary>
    private void OnConfigReplaced(object? sender, AppConfig config)
    {
        DebugLog.Info("Configuration replaced by an import");
        _main?.Reload(config);
        _appearance?.Reload(config);

        // The settings already registered the imported shortcut when it differed (and kept the previous one in the
        // configuration if it was refused): registering it again makes sure the one in effect is the one recorded.
        if (_settings is not null)
        {
            _settings.IsHotkeyRegistered = RegisterHotkey(config.Settings.Hotkey, reportFailure: false);
        }
    }

    /// <summary>
    /// The shortcut, routed by <see cref="ShortcutRouting"/>: it shows the hidden window, hides the window in front,
    /// brings back to the front a window left behind another one (Customize mode) with that window as the new calling
    /// window, and does nothing while one of FolderSwitch's own boxes is open. While the settings show their shortcut page it goes
    /// to the shortcut editor's test zone instead, which says it was recognized when the combination shown is the one in
    /// effect (see <see cref="HotkeyEditorViewModel.CanTest"/>).
    /// </summary>
    private void OnHotkeyTriggered(object? sender, EventArgs e)
    {
        if (_controller is not { } controller)
        {
            return;
        }

        var shown = controller.IsShown;
        var action = ShortcutRouting.Decide(
            windowShown: shown,
            ownWindowInFront: shown && controller.IsInFront,
            hotkeyTestShown: shown && _window is { MainContent.IsHotkeyTestShown: true },
            promptOpen: controller.IsPromptOpen);

        switch (action)
        {
            case ShortcutAction.TestZone:
                _hotkeyEditor?.NotifyTriggered();
                break;
            case ShortcutAction.Show:
                controller.Open();
                break;
            case ShortcutAction.Hide:
                controller.HideWindow();
                break;
            case ShortcutAction.Recall:
                controller.Recall();
                break;
        }
    }

    private void OnOpenRequested(object? sender, EventArgs e) => _controller?.Open();

    private void OnCustomizeRequested(object? sender, EventArgs e) => _controller?.ShowCustomize();

    private void OnQuitRequested(object? sender, EventArgs e)
    {
        DebugLog.Info("Quit from the notification menu");
        _quit();
    }

    /// <summary>
    /// A check found an update (the menu entry « Install update », and the notification when it is new to the user)
    /// or found none any more (the entry goes).
    /// </summary>
    private void OnUpdateAvailabilityChanged(object? sender, UpdateAvailability availability)
    {
        if (availability.Version is { } version)
        {
            _tray?.AnnounceUpdate(version, availability.Notify);
        }
        else
        {
            _tray?.ClearUpdate();
        }
    }

    private void OnUpdateDownloadingChanged(object? sender, bool downloading) => _tray?.SetUpdateDownloading(downloading);

    /// <summary>A check or a download is running or over: the « Check for updates » button is disabled meanwhile.</summary>
    private void OnUpdateBusyChanged(object? sender, EventArgs e)
    {
        if (_settings is not null && _updates is not null)
        {
            _settings.IsUpdateBusy = _updates.IsBusy;
        }
    }

    /// <summary>
    /// The menu entry or the notification of an update was clicked: the offer is made once the menu has closed (the
    /// offer is a modal box).
    /// </summary>
    private void OnInstallUpdateRequested(object? sender, EventArgs e) =>
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(OfferAnnouncedUpdate));

    /// <summary>
    /// The offer, as an <c>async void</c> handler on the dispatcher: an exception that <see cref="UpdateChecker"/> lets
    /// through (a critical one, see <see cref="ExceptionPolicy"/>) reaches <c>DispatcherUnhandledException</c>, which
    /// stops the process, instead of being lost in a task nobody observes.
    /// </summary>
    private async void OfferAnnouncedUpdate()
    {
        if (_updates is not null)
        {
            await _updates.OfferAnnouncedAsync();
        }
    }

    private void UnwireTriggers()
    {
        if (!_triggersWired)
        {
            return;
        }

        _triggersWired = false;
        _hotkeys!.Triggered -= OnHotkeyTriggered;
        _options.Instance.ActivationRequested -= OnOpenRequested;
        _tray!.OpenRequested -= OnOpenRequested;
        _tray.CustomizeRequested -= OnCustomizeRequested;
        _tray.QuitRequested -= OnQuitRequested;
        if (_updates is not null)
        {
            _updates.UpdateAvailabilityChanged -= OnUpdateAvailabilityChanged;
            _updates.DownloadingChanged -= OnUpdateDownloadingChanged;
            _tray.InstallUpdateRequested -= OnInstallUpdateRequested;
        }
    }

    private static void Release(string what, Action release)
    {
        try
        {
            release();
        }
        catch (Exception e)
        {
            DebugLog.Error($"Releasing the {what} failed", e);
        }
    }
}
