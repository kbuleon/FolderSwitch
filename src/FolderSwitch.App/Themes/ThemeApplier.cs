using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FolderSwitch.Core.Theming;
using FolderSwitch.Presentation.ViewModels;

namespace FolderSwitch.App.Themes;

/// <summary>
/// Turns the appearance (<see cref="AppearanceViewModel.Tokens"/> and <see cref="AppearanceViewModel.RadiusPx"/>) into
/// the named resources of <see cref="ThemeKeys"/> in the application's resources, and again on every
/// <see cref="AppearanceViewModel.AppearanceChanged"/>. Also installs the views' shared styles
/// (<c>Themes/Generic.xaml</c>) once.
/// </summary>
/// <remarks>
/// <para>
/// The resources of one appearance form one dictionary, merged into the target; a change replaces that dictionary as
/// a whole, so that the views are told once rather than once per resource. Every brush is frozen.
/// </para>
/// <para>
/// Besides the theme's tokens, a few colors are FolderSwitch's own and the same in every theme: the accent of
/// Customize mode (amber, with dark text, which reads at 4.5:1 or better on it where white would not) and the red of
/// the close cross under the mouse (the one of Windows, on which white reads at 4.6:1). The menus, the settings window
/// and the dialogs are drawn on <see cref="ThemeKeys.Menu"/> (the card), with their own text colors corrected against
/// it; their messages are tinted in red, amber, green or blue, the text corrected to 4.5:1 against the tint.
/// </para>
/// <para>UI thread only.</para>
/// </remarks>
internal sealed class ThemeApplier : IDisposable
{
    private const string AccentHex = "#f59e0b";
    private const string OnAccentHex = "#1c1c22";
    private const string CloseHoverHex = "#e81123";
    private const string WhiteHex = "#ffffff";
    private const string BlackHex = "#000000";
    private const string SwatchRingHex = "#808080";
    private const string DangerHex = "#ef4444";
    private const string SuccessHex = "#22c55e";
    private const string InfoHex = "#3b82f6";
    private const double TextMinRatio = 4.5;
    private const double MutedMinRatio = 3.0;

    /// <summary>An item's hover highlight is never rounder than this, whatever the group's corners.</summary>
    private const double ItemRadiusMax = 7;

    /// <summary>Side of the tile of the dotted board background.</summary>
    private const double DotPitch = 14;

    private static readonly Uri GenericUri = new("/FolderSwitch;component/Themes/Generic.xaml", UriKind.Relative);

    private readonly AppearanceViewModel _appearance;
    private readonly ResourceDictionary _target;
    private ResourceDictionary? _applied;
    private bool _disposed;

    /// <param name="target">Where the resources go: the application's resources.</param>
    public ThemeApplier(AppearanceViewModel appearance, ResourceDictionary target)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(target);

        _appearance = appearance;
        _target = target;

        InstallStyles(target);
        Apply(new AppearanceState(appearance.Theme, appearance.EffectiveVariant, appearance.Tokens, appearance.RadiusPx));
        appearance.AppearanceChanged += OnAppearanceChanged;
    }

    /// <summary>The appearance the resources describe now.</summary>
    public AppearanceState Current { get; private set; } = null!;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _appearance.AppearanceChanged -= OnAppearanceChanged;
    }

    /// <summary>The resources of <paramref name="state"/>, keyed by <see cref="ThemeKeys"/>.</summary>
    /// <exception cref="FormatException">A token is not a color (the catalog's never are).</exception>
    public static ResourceDictionary Build(AppearanceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var t = state.Tokens;
        var dark = state.Variant == Variant.Dark;
        var menu = MenuSurface(t, dark);
        var menuText = ColorMath.EnsureContrast(t.Text, menu, TextMinRatio);
        var menuMuted = ColorMath.EnsureContrast(t.Muted, menu, MutedMinRatio);
        var resources = new ResourceDictionary
        {
            [ThemeKeys.Background] = SolidBrush(t.Background),
            [ThemeKeys.Card] = SolidBrush(t.Card),
            [ThemeKeys.Text] = SolidBrush(t.Text),
            [ThemeKeys.Muted] = SolidBrush(t.Muted),
            [ThemeKeys.Border] = SolidBrush(t.Border),
            [ThemeKeys.OnBackgroundText] = SolidBrush(t.OnBackgroundText),
            [ThemeKeys.OnBackgroundMuted] = SolidBrush(t.OnBackgroundMuted),

            [ThemeKeys.GroupRadius] = new CornerRadius(state.RadiusPx),
            [ThemeKeys.ItemRadius] = new CornerRadius(Math.Min(ItemRadiusMax, state.RadiusPx)),
            [ThemeKeys.Dense] = t.Dense,
            [ThemeKeys.UppercaseTitles] = t.UppercaseTitles,
            [ThemeKeys.ColumnGap] = t.Dense ? 10.0 : 13.0,
            [ThemeKeys.GroupGap] = t.Dense ? 8.0 : 11.0,
            [ThemeKeys.ItemPadding] = t.Dense ? new Thickness(8, 3, 8, 3) : new Thickness(8, 5, 8, 5),
            [ThemeKeys.ItemFontSize] = t.Dense ? 12.8 : 13.75,
            [ThemeKeys.Appearance] = state,

            [ThemeKeys.WindowBorder] = SolidBrush(Flatten(t.Border, t.Background)),
            [ThemeKeys.Hover] = SolidBrush(t.OnBackgroundText, 0.05),
            [ThemeKeys.ButtonHoverBorder] = SolidBrush(t.Muted),
            [ThemeKeys.Accent] = SolidBrush(AccentHex),
            [ThemeKeys.OnAccent] = SolidBrush(ColorMath.EnsureContrast(OnAccentHex, AccentHex, TextMinRatio)),
            [ThemeKeys.CloseHover] = SolidBrush(CloseHoverHex),
            [ThemeKeys.OnCloseHover] = SolidBrush(ColorMath.EnsureContrast(WhiteHex, CloseHoverHex, TextMinRatio)),
            [ThemeKeys.BoardDots] = Dots(t.OnBackgroundText),
            [ThemeKeys.ColumnOutline] = SolidBrush(t.OnBackgroundText, 0.18),

            [ThemeKeys.GroupRadiusValue] = (double)state.RadiusPx,
            [ThemeKeys.AddOutline] = SolidBrush(t.OnBackgroundText, 0.26),
            [ThemeKeys.AddFill] = SolidBrush(t.Background, 0.7),
            [ThemeKeys.ResizeGrip] = SolidBrush(t.OnBackgroundText, 0.22),
            [ThemeKeys.SwatchRing] = SolidBrush(SwatchRingHex, 0.45),

            [ThemeKeys.Menu] = SolidBrush(menu),
            [ThemeKeys.MenuText] = SolidBrush(menuText),
            [ThemeKeys.MenuMuted] = SolidBrush(menuMuted),
            [ThemeKeys.MenuHover] = SolidBrush(menuText, 0.08),
            [ThemeKeys.MenuSelected] = SolidBrush(menuText, 0.06),
            [ThemeKeys.MenuSelectedBorder] = SolidBrush(menuText, 0.55),
            [ThemeKeys.SegmentTrack] = SolidBrush(menuText, 0.09),
            [ThemeKeys.KeyCap] = SolidBrush(ColorMath.Mix(menuText, dark ? 0.1 : 0.04, menu)),
            [ThemeKeys.KeyCapBorder] = SolidBrush(menuText, 0.22),
            [ThemeKeys.FlyoutShadow] = Shadow(dark),
        };

        AddMessageColors(resources, ThemeKeys.Danger, ThemeKeys.DangerBackground, DangerHex, menu, dark);
        AddMessageColors(resources, ThemeKeys.Warning, ThemeKeys.WarningBackground, AccentHex, menu, dark);
        AddMessageColors(resources, ThemeKeys.Success, ThemeKeys.SuccessBackground, SuccessHex, menu, dark);
        AddMessageColors(resources, ThemeKeys.Info, ThemeKeys.InfoBackground, InfoHex, menu, dark);
        return resources;
    }

    /// <summary>
    /// The surface of menus and dialogs: the card, except for Outline, whose card is the window background itself; its
    /// menus are then a little lighter than the background (white on a light one), as in the mockups.
    /// </summary>
    private static string MenuSurface(ThemeTokens t, bool dark) =>
        t.GroupStyle != GroupStyle.Outline
            ? t.Card
            : dark ? ColorMath.Mix(WhiteHex, 0.07, t.Background) : WhiteHex;

    /// <summary>A message's tint and its text: <paramref name="hex"/> over the menu, the text corrected to 4.5:1.</summary>
    private static void AddMessageColors(ResourceDictionary resources, string textKey, string backgroundKey, string hex, string menu, bool dark)
    {
        var background = ColorMath.Mix(hex, dark ? 0.2 : 0.12, menu);
        resources[backgroundKey] = SolidBrush(background);
        resources[textKey] = SolidBrush(ColorMath.EnsureContrast(hex, background, TextMinRatio));
    }

    /// <summary>The shadow of a menu or a dialog, like the mockups' <c>0 14px 34px rgba(0,0,0,.30)</c>.</summary>
    private static DropShadowEffect Shadow(bool dark)
    {
        var effect = new DropShadowEffect
        {
            Color = ToColor(BlackHex),
            Direction = 270,
            ShadowDepth = 8,
            BlurRadius = 26,
            Opacity = dark ? 0.55 : 0.28,
            RenderingBias = RenderingBias.Performance,
        };
        effect.Freeze();
        return effect;
    }

    /// <summary>A WPF color from <c>#RRGGBB</c> or <c>#AARRGGBB</c>, its alpha multiplied by <paramref name="opacity"/>.</summary>
    /// <exception cref="FormatException"><paramref name="hex"/> is not a color.</exception>
    public static Color ToColor(string hex, double opacity = 1)
    {
        if (!ColorMath.IsValidHex(hex))
        {
            throw new FormatException($"'{hex}' is not a color; expected #RRGGBB or #AARRGGBB.");
        }

        var argb = uint.Parse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        var alpha = hex.Length == 9 ? (byte)(argb >> 24) : (byte)255;
        return Color.FromArgb(
            (byte)Math.Round(alpha * Math.Clamp(opacity, 0, 1)),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);
    }

    /// <summary>A frozen brush of <paramref name="hex"/> at <paramref name="opacity"/>.</summary>
    public static SolidColorBrush SolidBrush(string hex, double opacity = 1)
    {
        var brush = new SolidColorBrush(ToColor(hex, opacity));
        brush.Freeze();
        return brush;
    }

    /// <summary>The opaque color a translucent <paramref name="color"/> shows over <paramref name="background"/>.</summary>
    private static string Flatten(string color, string background)
    {
        var alpha = ToColor(color).A / 255.0;
        return ColorMath.Mix(color, alpha, background);
    }

    /// <summary>One dot of <paramref name="hex"/> at 15 % every <see cref="DotPitch"/> DIPs.</summary>
    private static DrawingBrush Dots(string hex)
    {
        var tile = new DrawingGroup();
        tile.Children.Add(new GeometryDrawing(
            SolidBrush(hex, 0.15),
            null,
            new EllipseGeometry(new Point(DotPitch / 2, DotPitch / 2), 1.15, 1.15)));

        var brush = new DrawingBrush(tile)
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            Viewbox = new Rect(0, 0, DotPitch, DotPitch),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, DotPitch, DotPitch),
            ViewportUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Merges the views' shared styles into <paramref name="target"/>, once: before the first window whose XAML uses them
    /// (the first-run wizard's comes before any <see cref="ThemeApplier"/>).
    /// </summary>
    internal static void InstallStyles(ResourceDictionary target)
    {
        if (target.MergedDictionaries.Any(dictionary => dictionary.Source is { } source && source.OriginalString == GenericUri.OriginalString))
        {
            return;
        }

        target.MergedDictionaries.Insert(0, new ResourceDictionary { Source = GenericUri });
    }

    private void OnAppearanceChanged(object? sender, AppearanceChangedEventArgs e)
    {
        if (!_disposed)
        {
            Apply(new AppearanceState(_appearance.Theme, _appearance.EffectiveVariant, e.Tokens, e.RadiusPx));
        }
    }

    private void Apply(AppearanceState state)
    {
        var resources = Build(state);
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
        Current = state;
    }
}
