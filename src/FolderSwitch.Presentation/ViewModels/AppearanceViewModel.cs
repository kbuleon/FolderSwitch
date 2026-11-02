// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using CommunityToolkit.Mvvm.ComponentModel;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Services;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.Services;

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>
/// The appearance menu: mode, theme, background color and group corners, written to <c>config.Appearance</c>.
/// <para>
/// Every change is written to the configuration, which is then saved exactly once; a value already in place, or
/// one that is not valid, changes and saves nothing. Whenever what the window is drawn with changes (a change of
/// the user's, or of the Windows mode while in <see cref="ThemeMode.Auto"/>), <see cref="AppearanceChanged"/> is
/// raised with the new tokens. Meant to be used from the UI thread, <see cref="ISystemInfo.ThemeChanged"/> included.
/// </para>
/// </summary>
public sealed class AppearanceViewModel : ObservableObject, IDisposable
{
    /// <summary>Key of the first entry of <see cref="BackgroundChoices"/>: the theme's own background.</summary>
    public const string DefaultBackgroundKey = "default";

    private const string FallbackTheme = "fluent";

    private readonly ISystemInfo _system;
    private readonly IConfigPersistence _persistence;

    private AppConfig _config;
    private Variant _variant;
    private IReadOnlyList<NamedColor> _backgroundChoices;
    private string _background;
    private ThemeTokens _tokens;
    private int _radiusPx;
    private bool _disposed;

    public AppearanceViewModel(AppConfig config, ISystemInfo system, IConfigPersistence persistence)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(persistence);

        _config = config;
        _system = system;
        _persistence = persistence;

        _variant = ResolveVariant();
        _backgroundChoices = BuildBackgroundChoices();
        _background = ResolveBackground();
        _tokens = ResolveTokens();
        _radiusPx = ResolveRadius();

        _system.ThemeChanged += OnSystemThemeChanged;
    }

    /// <summary>Raised when the colors or the group corner radius the window is drawn with change.</summary>
    public event EventHandler<AppearanceChangedEventArgs>? AppearanceChanged;

    /// <summary>Light, Dark, or Auto (follows the Windows application mode, live).</summary>
    public ThemeMode Mode
    {
        get => Appearance.Mode;
        set
        {
            if (!Enum.IsDefined(value) || value == Appearance.Mode)
            {
                return;
            }

            Appearance.Mode = value;
            OnPropertyChanged();
            Refresh();
            Save();
        }
    }

    /// <summary>
    /// Key of the theme (see <see cref="ThemeCatalog.ThemeKeys"/>). A theme the catalog does not know (a hand-edited
    /// configuration) reads as <c>fluent</c>, which is what is drawn; setting an unknown key is ignored.
    /// </summary>
    public string Theme
    {
        get => EffectiveTheme;
        set
        {
            if (value is null || !ThemeCatalog.ThemeKeys.Contains(value)
                || string.Equals(value, Appearance.Theme, StringComparison.Ordinal))
            {
                return;
            }

            Appearance.Theme = value;
            OnPropertyChanged();
            Refresh();
            Save();
        }
    }

    /// <summary>The variant drawn: the mode itself, or for <see cref="ThemeMode.Auto"/> the Windows application mode.</summary>
    public Variant EffectiveVariant => _variant;

    /// <summary>
    /// The backgrounds offered for the theme in the effective variant: first the theme's own background (key
    /// <see cref="DefaultBackgroundKey"/>), then the catalog tints of the variant, minus the one equal to it.
    /// </summary>
    public IReadOnlyList<NamedColor> BackgroundChoices => _backgroundChoices;

    /// <summary>
    /// The window background, <c>#rrggbb</c>, remembered for each theme and variant (key <c>theme:light</c> or
    /// <c>theme:dark</c> of <c>Appearance.Backgrounds</c>); the theme's own background when none is remembered.
    /// Any <c>#RRGGBB</c> / <c>#AARRGGBB</c> color is accepted (the alpha is dropped), anything else is ignored;
    /// choosing the theme's own background forgets the remembered one.
    /// </summary>
    public string Background
    {
        get => _background;
        set
        {
            if (!ColorMath.IsValidHex(value))
            {
                return;
            }

            var color = ToRrggbb(value);
            var key = BackgroundKey;
            if (string.Equals(color, DefaultBackground, StringComparison.Ordinal))
            {
                if (!Appearance.Backgrounds.Remove(key))
                {
                    return;
                }
            }
            else
            {
                if (Appearance.Backgrounds.TryGetValue(key, out var stored) && string.Equals(stored, color, StringComparison.Ordinal))
                {
                    return;
                }

                Appearance.Backgrounds[key] = color;
            }

            Refresh();
            Save();
        }
    }

    /// <summary>Group corners, for every theme.</summary>
    public GroupRadius Radius
    {
        get => Appearance.Radius;
        set
        {
            if (!Enum.IsDefined(value) || value == Appearance.Radius)
            {
                return;
            }

            Appearance.Radius = value;
            OnPropertyChanged();
            Refresh();
            Save();
        }
    }

    /// <summary>The tokens the window is drawn with now: the value <see cref="AppearanceChanged"/> last carried.</summary>
    public ThemeTokens Tokens => _tokens;

    /// <summary>Group corner radius in px now: the value <see cref="AppearanceChanged"/> last carried.</summary>
    public int RadiusPx => _radiusPx;

    private Appearance Appearance => _config.Appearance;

    private string EffectiveTheme =>
        ThemeCatalog.ThemeKeys.Contains(Appearance.Theme) ? Appearance.Theme : FallbackTheme;

    private string BackgroundKey => EffectiveTheme + (_variant == Variant.Dark ? ":dark" : ":light");

    private string DefaultBackground => ThemeCatalog.Get(EffectiveTheme, _variant).Background;

    /// <summary>
    /// Edits another configuration from now on (after an import). Raises the changes it brings, including
    /// <see cref="AppearanceChanged"/>; saves nothing.
    /// </summary>
    public void Reload(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(Radius));
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _system.ThemeChanged -= OnSystemThemeChanged;
    }

    /// <summary><c>#rrggbb</c> form of a valid color: the last six digits are the color in both accepted forms.</summary>
    private static string ToRrggbb(string validHex) => "#" + validHex[^6..].ToLowerInvariant();

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            Refresh();
        }
    }

    /// <summary>Brings the derived values in line with the configuration and the system, raising what changed.</summary>
    private void Refresh()
    {
        var variant = ResolveVariant();
        if (variant != _variant)
        {
            _variant = variant;
            OnPropertyChanged(nameof(EffectiveVariant));
        }

        var choices = BuildBackgroundChoices();
        if (!choices.SequenceEqual(_backgroundChoices))
        {
            _backgroundChoices = choices;
            OnPropertyChanged(nameof(BackgroundChoices));
        }

        var background = ResolveBackground();
        if (!string.Equals(background, _background, StringComparison.Ordinal))
        {
            _background = background;
            OnPropertyChanged(nameof(Background));
        }

        var tokens = ResolveTokens();
        var radiusPx = ResolveRadius();
        var tokensChanged = tokens != _tokens;
        var radiusChanged = radiusPx != _radiusPx;
        if (!tokensChanged && !radiusChanged)
        {
            return;
        }

        _tokens = tokens;
        _radiusPx = radiusPx;
        if (tokensChanged)
        {
            OnPropertyChanged(nameof(Tokens));
        }

        if (radiusChanged)
        {
            OnPropertyChanged(nameof(RadiusPx));
        }

        AppearanceChanged?.Invoke(this, new AppearanceChangedEventArgs(tokens, radiusPx));
    }

    private Variant ResolveVariant() => Appearance.Mode switch
    {
        ThemeMode.Dark => Variant.Dark,
        ThemeMode.Auto => _system.AppsUseDarkTheme ? Variant.Dark : Variant.Light,
        _ => Variant.Light,
    };

    private IReadOnlyList<NamedColor> BuildBackgroundChoices()
    {
        var themeDefault = DefaultBackground;
        var choices = new List<NamedColor> { new(DefaultBackgroundKey, themeDefault) };
        choices.AddRange(ThemeCatalog.Backgrounds(_variant)
            .Where(color => !string.Equals(color.Hex, themeDefault, StringComparison.OrdinalIgnoreCase)));
        return choices.AsReadOnly();
    }

    /// <summary>The remembered background when it is a color (a hand-edited one may not be), else the theme's.</summary>
    private string ResolveBackground() =>
        Appearance.Backgrounds.TryGetValue(BackgroundKey, out var stored) && ColorMath.IsValidHex(stored)
            ? ToRrggbb(stored)
            : DefaultBackground;

    private ThemeTokens ResolveTokens() => ThemeCatalog.Resolve(EffectiveTheme, _variant, _background);

    private int ResolveRadius() => ThemeCatalog.RadiusPx(Appearance.Radius, EffectiveTheme);

    private void Save() => _persistence.Save(_config);
}
