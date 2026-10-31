using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace FolderSwitch.App.Views;

/// <summary>
/// The About window: what FolderSwitch is and which version runs. Shown by <see cref="MainView"/> over the window; its
/// ✕ calls the <c>close</c> it was given.
/// </summary>
public partial class AboutView : UserControl, IDisposable
{
    private static readonly DependencyPropertyKey VersionTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VersionText), typeof(string), typeof(AboutView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VersionTextProperty = VersionTextPropertyKey.DependencyProperty;

    private readonly Action _close;
    private bool _disposed;

    internal AboutView(ViewTexts texts, Action close)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        Texts = texts;
        _close = close;
        InitializeComponent();
        Icon.Source = AppIconDrawing.Glyph;

        UpdateVersion();
        texts.PropertyChanged += OnTextsChanged;
    }

    public ViewTexts Texts { get; }

    /// <summary>« Version 1.0.0 », in the current language.</summary>
    public string VersionText => (string)GetValue(VersionTextProperty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Texts.PropertyChanged -= OnTextsChanged;
    }

    private void OnTextsChanged(object? sender, PropertyChangedEventArgs e) => UpdateVersion();

    private void UpdateVersion() =>
        SetValue(VersionTextPropertyKey, string.Format(CultureInfo.CurrentCulture, Texts["Settings.Version"], SettingsView.Version));

    private void OnCloseClick(object sender, RoutedEventArgs e) => _close();
}
