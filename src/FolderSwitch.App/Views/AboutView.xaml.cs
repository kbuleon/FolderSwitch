// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FolderSwitch.Core.Services;

namespace FolderSwitch.App.Views;

/// <summary>
/// The About window: what FolderSwitch is, which version runs, and its legal notices (copyright, license, no warranty).
/// Shown by <see cref="MainView"/> over the window; its ✕ calls the <c>close</c> it was given.
/// </summary>
/// <remarks>
/// Its two links open <see cref="LicenseFile"/> and <see cref="NoticesFile"/>, copied next to the executable by the
/// build, with the <c>shell</c> it was given (none: the links do nothing, as in a snapshot). They work offline and
/// match the version installed.
/// </remarks>
public partial class AboutView : UserControl, IDisposable
{
    private static readonly DependencyPropertyKey VersionTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VersionText), typeof(string), typeof(AboutView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty VersionTextProperty = VersionTextPropertyKey.DependencyProperty;

    /// <summary>The GPLv3 text, next to the executable (with an extension: the shell opens it in the text editor).</summary>
    internal const string LicenseFile = "LICENSE.txt";

    /// <summary>The licenses of the components distributed with FolderSwitch, next to the executable.</summary>
    internal const string NoticesFile = "THIRD-PARTY-NOTICES.txt";

    private readonly IShellLauncher? _shell;
    private readonly Action _close;
    private bool _disposed;

    internal AboutView(ViewTexts texts, IShellLauncher? shell, Action close)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(close);

        Texts = texts;
        _shell = shell;
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

    private void OnLicenseClick(object sender, RoutedEventArgs e) => Open(LicenseFile);

    private void OnNoticesClick(object sender, RoutedEventArgs e) => Open(NoticesFile);

    /// <summary>Opens the file with its application; a failure is the shell's to tell (<see cref="IShellLauncher.OpenFile"/>).</summary>
    private void Open(string file) => _shell?.OpenFile(Path.Combine(AppContext.BaseDirectory, file));
}
