using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>A category of the settings window.</summary>
public enum SettingsPage
{
    General,
    Hotkey,
    Appearance,
    Configuration,
}

/// <summary>A category in the list of the settings window, with its icon (a glyph of the icon font).</summary>
public sealed record SettingsPageChoice(SettingsPage Page, string Glyph, string Label, bool IsSelected);

/// <summary>
/// The settings window, bound to <see cref="SettingsViewModel"/>: every setting of FolderSwitch in one place, by
/// category (« at a common place », spec 5.4). Shown by <see cref="MainView"/> over the window; its ✕ calls the
/// <c>close</c> it was given. The appearance page is the appearance menu (<see cref="AppearanceMenu"/>), built the
/// first time it is shown.
/// </summary>
/// <remarks>
/// Leaving the shortcut page ends a capture in progress (the keys pressed next are not meant for it); showing it starts
/// its test zone again, waiting for a press.
/// </remarks>
public partial class SettingsView : UserControl, IDisposable
{
    public static readonly DependencyProperty PagesProperty = DependencyProperty.Register(
        nameof(Pages), typeof(IReadOnlyList<SettingsPageChoice>), typeof(SettingsView));

    private static readonly DependencyPropertyKey VersionTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VersionText), typeof(string), typeof(SettingsView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VersionTextProperty = VersionTextPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey AppliedNotePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(AppliedNote), typeof(HotkeyNote), typeof(SettingsView), new PropertyMetadata(null));

    public static readonly DependencyProperty AppliedNoteProperty = AppliedNotePropertyKey.DependencyProperty;

    private readonly AppearanceViewModel _appearance;
    private readonly Action _close;
    private AppearanceMenu? _appearanceMenu;
    private bool _disposed;

    internal SettingsView(SettingsViewModel settings, AppearanceViewModel appearance, ViewTexts texts, SettingsPage page, Action close)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        Settings = settings;
        Texts = texts;
        _appearance = appearance;
        _close = close;
        DataContext = settings;
        InitializeComponent();

        Show(page);
        UpdateTexts();
        texts.PropertyChanged += OnTextsChanged;
        settings.PropertyChanged += OnSettingsChanged;
    }

    public SettingsViewModel Settings { get; }

    public ViewTexts Texts { get; }

    /// <summary>The category shown.</summary>
    public SettingsPage Page { get; private set; }

    public IReadOnlyList<SettingsPageChoice>? Pages
    {
        get => (IReadOnlyList<SettingsPageChoice>?)GetValue(PagesProperty);
        private set => SetValue(PagesProperty, value);
    }

    /// <summary>« Version 1.0.0 », in the current language.</summary>
    public string VersionText => (string)GetValue(VersionTextProperty);

    /// <summary>« ✓ This shortcut is active. » while the combination shown is the one in effect; <c>null</c> otherwise.</summary>
    public HotkeyNote? AppliedNote => (HotkeyNote?)GetValue(AppliedNoteProperty);

    /// <summary>The version of FolderSwitch: the application's assembly version, three parts.</summary>
    internal static string Version => typeof(SettingsView).Assembly.GetName().Version?.ToString(3) ?? "?";

    /// <summary>Shows the category <paramref name="page"/>.</summary>
    internal void Show(SettingsPage page)
    {
        if (page != SettingsPage.Hotkey)
        {
            Settings.Hotkey.StopCapture();
        }
        else if (HotkeyPage.Visibility != Visibility.Visible)
        {
            Settings.Hotkey.ResetTest();
        }

        Page = page;
        if (page == SettingsPage.Appearance && _appearanceMenu is null)
        {
            _appearanceMenu = new AppearanceMenu(_appearance, Texts);
            AppearancePage.Content = _appearanceMenu;
        }

        GeneralPage.Visibility = VisibleIf(page == SettingsPage.General);
        HotkeyPage.Visibility = VisibleIf(page == SettingsPage.Hotkey);
        AppearancePage.Visibility = VisibleIf(page == SettingsPage.Appearance);
        ConfigurationPage.Visibility = VisibleIf(page == SettingsPage.Configuration);
        UpdatePages();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Texts.PropertyChanged -= OnTextsChanged;
        Settings.PropertyChanged -= OnSettingsChanged;
        Settings.Hotkey.StopCapture();
        _appearanceMenu?.Dispose();

        // The editor follows the language through the texts: it lets go of them.
        Editor.Texts = null;
    }

    private static Visibility VisibleIf(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void UpdatePages() =>
        Pages =
        [
            Choice(SettingsPage.General, "", "Settings.General"),
            Choice(SettingsPage.Hotkey, "", "Settings.Hotkey"),
            Choice(SettingsPage.Appearance, "", "Window.Appearance"),
            Choice(SettingsPage.Configuration, "", "Settings.Configuration"),
        ];

    private SettingsPageChoice Choice(SettingsPage page, string glyph, string key) => new(page, glyph, Texts[key], page == Page);

    private void UpdateTexts()
    {
        SetValue(VersionTextPropertyKey, string.Format(CultureInfo.CurrentCulture, Texts["Settings.Version"], Version));
        UpdateAppliedNote();
        UpdatePages();
    }

    private void UpdateAppliedNote() =>
        SetValue(AppliedNotePropertyKey, Settings.IsHotkeyApplied ? new HotkeyNote("✓ " + Texts["Hotkey.InEffect"], IsConfirmation: true) : null);

    private void OnTextsChanged(object? sender, PropertyChangedEventArgs e) => UpdateTexts();

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(SettingsViewModel.IsHotkeyApplied))
        {
            UpdateAppliedNote();
        }
    }

    private void OnPageClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SettingsPageChoice choice })
        {
            Show(choice.Page);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => _close();
}
