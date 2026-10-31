using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Views;

/// <summary>
/// The settings panel, bound to <see cref="SettingsViewModel"/>. Placed in the title bar's ⚙ flyout (« at a common
/// place » of Customize mode, spec 5.4): next to the appearance menu, out of the board's way.
/// </summary>
public partial class SettingsPanel : UserControl, IDisposable
{
    private static readonly DependencyPropertyKey VersionTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VersionText), typeof(string), typeof(SettingsPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VersionTextProperty = VersionTextPropertyKey.DependencyProperty;

    private bool _disposed;

    internal SettingsPanel(SettingsViewModel settings, ViewTexts texts)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(texts);

        Settings = settings;
        Texts = texts;
        DataContext = settings;
        InitializeComponent();

        UpdateVersion();
        texts.PropertyChanged += OnTextsChanged;
    }

    public SettingsViewModel Settings { get; }

    public ViewTexts Texts { get; }

    /// <summary>« Version 1.0.0 », in the current language.</summary>
    public string VersionText => (string)GetValue(VersionTextProperty);

    /// <summary>The version of FolderSwitch: the application's assembly version, three parts.</summary>
    internal static string Version => typeof(SettingsPanel).Assembly.GetName().Version?.ToString(3) ?? "?";

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Texts.PropertyChanged -= OnTextsChanged;

        // The editor follows the language through the texts: it lets go of them.
        Editor.Texts = null;
    }

    private void OnTextsChanged(object? sender, PropertyChangedEventArgs e) => UpdateVersion();

    private void UpdateVersion() =>
        SetValue(VersionTextPropertyKey, string.Format(CultureInfo.CurrentCulture, Texts["Settings.Version"], Version));
}
