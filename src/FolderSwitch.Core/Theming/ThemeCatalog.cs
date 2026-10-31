using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Theming;

/// <summary>The seven themes, their two variants, the background and group color palettes.</summary>
public static class ThemeCatalog
{
    private const string FallbackTheme = "fluent";

    // Base colors shared by every theme (spec 5.5); a theme only overrides what the table below lists.
    private const string LightText = "#1c1c22";
    private const string LightMuted = "#74747f";
    private const string LightBorder = "#1a000000"; // black at 10 %
    private const string LightCard = "#ffffff";
    private const string DarkText = "#f1f1f5";
    private const string DarkMuted = "#a2a4b0";
    private const string DarkBorder = "#1cffffff"; // white at 11 %
    private const string DarkCard = "#2b2c33";

    private const double TextMinRatio = 4.5;
    private const double MutedMinRatio = 3.0;

    private static readonly Dictionary<string, ThemeSpec> Specs = new(StringComparer.Ordinal)
    {
        ["fluent"] = new(
            10, GroupStyle.LeftAccent, Dense: false, UppercaseTitles: false,
            Light: new("#f7f5f0"),
            Dark: new("#1c1c1c", Card: "#2d2d2d")),
        ["cards"] = new(
            14, GroupStyle.SolidHeader, Dense: false, UppercaseTitles: false,
            Light: new("#f7f5f0", Border: "#e3ded4"),
            Dark: new("#16171c", Card: "#262830")),
        ["pastel"] = new(
            16, GroupStyle.Tinted, Dense: false, UppercaseTitles: false,
            Light: new("#ffffff"),
            Dark: new("#15161a", Card: "#1f2026")),
        ["paper"] = new(
            4, GroupStyle.Underline, Dense: false, UppercaseTitles: true,
            Light: new("#ffffff", Border: "#e2e2e7"),
            Dark: new("#141518", Card: "#1d1e22", Border: "#34353c")),
        ["outline"] = new(
            12, GroupStyle.Outline, Dense: false, UppercaseTitles: false,
            Light: new("#fafafa"),
            Dark: new("#121317")),
        ["neon"] = new(
            10, GroupStyle.Glow, Dense: false, UppercaseTitles: false,
            Light: new("#f4f5f8"),
            Dark: new("#0e0f13", Card: "#15161c")),
        ["compact"] = new(
            7, GroupStyle.TopBar, Dense: true, UppercaseTitles: true,
            Light: new("#eef0f3", Border: "#d9dce3"),
            Dark: new("#0e0f13", Card: "#2b2d37", Border: "#424654")),
    };

    private static readonly NamedColor[] LightBackgrounds =
    [
        new("white", "#ffffff"),
        new("offWhite", "#fafafa"),
        new("cream", "#f7f5f0"),
        new("pearl", "#eef0f3"),
        new("glacier", "#eaf1fb"),
        new("mint", "#eaf5ef"),
        new("blush", "#f9eef0"),
    ];

    private static readonly NamedColor[] DarkBackgrounds =
    [
        new("black", "#0e0f13"),
        new("charcoal", "#16171c"),
        new("anthracite", "#1c1c1c"),
        new("graphite", "#24262c"),
        new("slate", "#1b2130"),
        new("midnight", "#121a2b"),
        new("forest", "#13201a"),
        new("aubergine", "#1e1626"),
    ];

    private static readonly NamedColor[] Palette =
    [
        new("blue", "#3b82f6"),
        new("cyan", "#06b6d4"),
        new("green", "#22c55e"),
        new("olive", "#84cc16"),
        new("amber", "#f59e0b"),
        new("orange", "#f97316"),
        new("red", "#ef4444"),
        new("pink", "#ec4899"),
        new("violet", "#8b5cf6"),
        new("slate", "#64748b"),
    ];

    private static readonly IReadOnlyList<NamedColor> LightBackgroundList = Array.AsReadOnly(LightBackgrounds);
    private static readonly IReadOnlyList<NamedColor> DarkBackgroundList = Array.AsReadOnly(DarkBackgrounds);

    /// <summary>Theme keys in display order: fluent, cards, pastel, paper, outline, neon, compact.</summary>
    public static IReadOnlyList<string> ThemeKeys { get; } =
        Array.AsReadOnly(["fluent", "cards", "pastel", "paper", "outline", "neon", "compact"]);

    /// <summary>The ten group colors, in display order.</summary>
    public static IReadOnlyList<NamedColor> GroupPalette { get; } = Array.AsReadOnly(Palette);

    /// <summary>Base text color of a variant, before any adaptation to a background.</summary>
    internal static string BaseText(Variant v) => v == Variant.Dark ? DarkText : LightText;

    /// <summary>The window backgrounds the user can pick for a variant.</summary>
    public static IReadOnlyList<NamedColor> Backgrounds(Variant v) =>
        v == Variant.Dark ? DarkBackgroundList : LightBackgroundList;

    /// <summary>
    /// Group corner radius in px: a fixed value, or the theme's own radius for
    /// <see cref="GroupRadius.Theme"/>. An unknown theme key behaves like <c>fluent</c>.
    /// </summary>
    public static int RadiusPx(GroupRadius r, string theme) => r switch
    {
        GroupRadius.Square => 0,
        GroupRadius.Slight => 4,
        GroupRadius.Medium => 10,
        GroupRadius.Strong => 18,
        _ => SpecFor(theme).Radius,
    };

    /// <summary>
    /// Default tokens of a theme in a variant. An unknown theme key (hand-edited configuration)
    /// falls back to <c>fluent</c>.
    /// </summary>
    public static ThemeTokens Get(string theme, Variant v)
    {
        var spec = SpecFor(theme);
        var dark = v == Variant.Dark;
        var side = dark ? spec.Dark : spec.Light;

        var card = spec.Style == GroupStyle.Outline
            ? side.Background // Outline draws groups straight on the window: no surface of its own.
            : side.Card ?? (dark ? DarkCard : LightCard);
        var text = dark ? DarkText : LightText;
        var muted = dark ? DarkMuted : LightMuted;

        return new ThemeTokens(
            Background: side.Background,
            Card: card,
            Text: text,
            Muted: muted,
            Border: side.Border ?? (dark ? DarkBorder : LightBorder),
            DefaultRadius: spec.Radius,
            GroupStyle: spec.Style,
            Dense: spec.Dense,
            UppercaseTitles: spec.UppercaseTitles,
            OnBackgroundText: text,
            OnBackgroundMuted: muted);
    }

    /// <summary>
    /// Tokens with the user's background applied. <c>OnBackgroundText</c> / <c>OnBackgroundMuted</c>
    /// (text drawn directly on the window background) are corrected so they stay readable on it
    /// (4.5:1 and 3:1). <c>Text</c> / <c>Muted</c> and <c>Card</c> are unchanged, except for Outline,
    /// whose groups sit on the window background: its card follows the background and its
    /// <c>Text</c> / <c>Muted</c> follow the on-background pair. A null or malformed override
    /// is ignored.
    /// </summary>
    public static ThemeTokens Resolve(string theme, Variant v, string? backgroundOverride)
    {
        var tokens = Get(theme, v);

        var background = backgroundOverride is not null && ColorMath.IsValidHex(backgroundOverride)
            ? ColorMath.Normalize(backgroundOverride)
            : tokens.Background;

        var onBackgroundText = ColorMath.EnsureContrast(tokens.Text, background, TextMinRatio);
        var onBackgroundMuted = ColorMath.EnsureContrast(tokens.Muted, background, MutedMinRatio);

        tokens = tokens with
        {
            Background = background,
            OnBackgroundText = onBackgroundText,
            OnBackgroundMuted = onBackgroundMuted,
        };

        return tokens.GroupStyle == GroupStyle.Outline
            ? tokens with { Card = background, Text = onBackgroundText, Muted = onBackgroundMuted }
            : tokens;
    }

    private static ThemeSpec SpecFor(string theme) =>
        theme is not null && Specs.TryGetValue(theme, out var spec) ? spec : Specs[FallbackTheme];

    /// <summary>Per-variant overrides of the base colors; null means "use the base value".</summary>
    private sealed record SideSpec(string Background, string? Card = null, string? Border = null);

    private sealed record ThemeSpec(
        int Radius,
        GroupStyle Style,
        bool Dense,
        bool UppercaseTitles,
        SideSpec Light,
        SideSpec Dark);
}
