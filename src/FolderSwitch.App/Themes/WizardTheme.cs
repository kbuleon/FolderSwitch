// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Windows;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Theming;
using ThemeMode = FolderSwitch.Core.Model.ThemeMode;

namespace FolderSwitch.App.Themes;

/// <summary>
/// The look of the first-run wizard, which comes before the configuration it creates and so before any
/// <see cref="ThemeApplier"/>: the resources of <see cref="ThemeKeys"/> for the mode and theme the wizard shows (the
/// theme's own background and corners), merged into the application's resources, so that its message boxes and its
/// language list are drawn alike, until <see cref="Dispose"/> takes them out again. Auto follows the Windows mode, live.
/// Also installs the views' shared styles, which the wizard's XAML uses.
/// </summary>
/// <remarks>
/// <para>
/// The wizard's accent is the blue of FolderSwitch's icon, as in the wizard's mockup, instead of the amber of Customize
/// mode: the progress bar, « Next », the capture frame of the shortcut, the check boxes. White text reads at 5.2:1 on
/// it.
/// </para>
/// <para>UI thread only, <see cref="ISystemInfo.ThemeChanged"/> included.</para>
/// </remarks>
internal sealed class WizardTheme : IDisposable
{
    private const string AccentHex = "#2563eb";
    private const string OnAccentHex = "#ffffff";
    private const double TextMinRatio = 4.5;

    private readonly ResourceDictionary _target;
    private readonly ISystemInfo _system;
    private ResourceDictionary? _applied;
    private ThemeMode _mode = ThemeMode.Light;
    private string _theme = "fluent";
    private bool _disposed;

    /// <param name="target">Where the resources go: the application's resources.</param>
    public WizardTheme(ResourceDictionary target, ISystemInfo system)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(system);

        _target = target;
        _system = system;

        ThemeApplier.InstallStyles(target);
        _system.ThemeChanged += OnSystemThemeChanged;
    }

    /// <summary>Draws the wizard in <paramref name="mode"/> and <paramref name="theme"/> (a key of the catalog).</summary>
    public void Apply(ThemeMode mode, string theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (_disposed)
        {
            return;
        }

        _mode = mode;
        _theme = theme;

        var resources = Build(theme, VariantOf(mode, _system.AppsUseDarkTheme));
        var index = _applied is null ? -1 : _target.MergedDictionaries.IndexOf(_applied);
        if (index >= 0)
        {
            _target.MergedDictionaries[index] = resources;
        }
        else
        {
            _target.MergedDictionaries.Add(resources);
        }

        _applied = resources;
    }

    /// <summary>The variant drawn: the mode itself, or for Auto the Windows application mode.</summary>
    public static Variant VariantOf(ThemeMode mode, bool appsUseDarkTheme) => mode switch
    {
        ThemeMode.Dark => Variant.Dark,
        ThemeMode.Auto => appsUseDarkTheme ? Variant.Dark : Variant.Light,
        _ => Variant.Light,
    };

    /// <summary>The wizard's resources for <paramref name="theme"/> in <paramref name="variant"/> (also drawn by the snapshots).</summary>
    public static ResourceDictionary Build(string theme, Variant variant)
    {
        var tokens = ThemeCatalog.Resolve(theme, variant, backgroundOverride: null);
        var resources = ThemeApplier.Build(new AppearanceState(theme, variant, tokens, ThemeCatalog.RadiusPx(GroupRadius.Theme, theme)));
        resources[ThemeKeys.Accent] = ThemeApplier.SolidBrush(AccentHex);
        resources[ThemeKeys.OnAccent] = ThemeApplier.SolidBrush(ColorMath.EnsureContrast(OnAccentHex, AccentHex, TextMinRatio));
        return resources;
    }

    /// <summary>Takes the wizard's resources out of the application's and stops following the Windows mode.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _system.ThemeChanged -= OnSystemThemeChanged;
        if (_applied is not null)
        {
            _target.MergedDictionaries.Remove(_applied);
            _applied = null;
        }
    }

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (!_disposed && _mode == ThemeMode.Auto)
        {
            Apply(_mode, _theme);
        }
    }
}
