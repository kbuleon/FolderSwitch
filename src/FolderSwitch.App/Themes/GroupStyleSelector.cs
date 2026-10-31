using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FolderSwitch.Core.Theming;

namespace FolderSwitch.App.Themes;

/// <summary>
/// How one group block is drawn: every brush, thickness and effect its template reads, for one group color in one
/// appearance. The template is the same for every <see cref="GroupStyle"/>; this is what differs.
/// </summary>
/// <remarks>
/// The block is drawn in layers, back to front: <see cref="Shadow"/> (cast by a plain shape of
/// <see cref="ShadowFill"/>, so that the effect never touches the text), then, clipped to the group's rounded corners,
/// the body (<see cref="BodyBackground"/>) with the header and the items inset by <see cref="ContentInset"/>, the frame
/// (<see cref="FrameBrush"/>, <see cref="FrameThickness"/>) and the colored edges (<see cref="LeftAccentWidth"/>,
/// <see cref="TopAccentHeight"/>), which are the group's outermost line.
/// </remarks>
internal sealed record GroupLook
{
    public required Brush HeaderBackground { get; init; }

    public required Brush HeaderText { get; init; }

    /// <summary>The group's colored edge, underline, frame or top bar, depending on the style.</summary>
    public required Brush Accent { get; init; }

    public required Brush BodyBackground { get; init; }

    public required Brush FolderIcon { get; init; }

    public required Brush FileIcon { get; init; }

    /// <summary>Highlight of an item under the mouse.</summary>
    public required Brush ItemHover { get; init; }

    public required Brush ItemText { get; init; }

    public required Brush ItemMuted { get; init; }

    /// <summary>The chevron and the grip of the header.</summary>
    public required Brush HeaderGlyph { get; init; }

    /// <summary>The item count: at least 4.5:1 against <see cref="CountBackground"/> (<see cref="GroupVisuals.CountBadge"/>).</summary>
    public required Brush CountText { get; init; }

    /// <summary>The count's pill, opaque: its tint already composited over the header.</summary>
    public required Brush CountBackground { get; init; }

    /// <summary>Ring around the color dot of Customize mode: the card around the color.</summary>
    public required Brush DotRing { get; init; }

    public required Brush FrameBrush { get; init; }

    public required Thickness FrameThickness { get; init; }

    /// <summary>What the frame and the colored edges take from the content (a ring drawn over the edge takes nothing).</summary>
    public required Thickness ContentInset { get; init; }

    public required double LeftAccentWidth { get; init; }

    public required double TopAccentHeight { get; init; }

    /// <summary>Underline of the header (Paper), drawn in <see cref="Accent"/>.</summary>
    public required Thickness HeaderUnderline { get; init; }

    public required Thickness HeaderPadding { get; init; }

    public required double TitleFontSize { get; init; }

    public required bool UppercaseTitle { get; init; }

    public Effect? Shadow { get; init; }

    public required Brush ShadowFill { get; init; }

    /// <summary>Glow of the title (Neon, dark).</summary>
    public Effect? TitleEffect { get; init; }
}

/// <summary>
/// Selects the <see cref="GroupLook"/> of a group: the colors of <see cref="GroupVisuals.For"/> for its color, laid out
/// the way the theme's <see cref="GroupStyle"/> draws a group (spec 5.5 and the theme mockups).
/// </summary>
internal static class GroupStyleSelector
{
    /// <summary>A group whose color cannot be read (a hand-edited configuration) is drawn in slate.</summary>
    public const string FallbackColor = "#64748b";

    private const string White = "#ffffff";
    private const string Black = "#000000";

    private const double TitleSize = 13.75;
    private static readonly Thickness DefaultHeaderPadding = new(10, 7, 10, 7);

    /// <summary>The look of a group of color <paramref name="groupHex"/> drawn in <paramref name="appearance"/>.</summary>
    /// <param name="collapsed">Whether the group is collapsed (Paper hides its underline then).</param>
    public static GroupLook Select(AppearanceState appearance, string? groupHex, bool collapsed)
    {
        ArgumentNullException.ThrowIfNull(appearance);

        var tokens = appearance.Tokens;
        var dark = appearance.Variant == Variant.Dark;
        var g = groupHex is not null && ColorMath.IsValidHex(groupHex) ? groupHex : FallbackColor;
        var visual = GroupVisuals.For(appearance.Theme, appearance.Variant, g, tokens.Card);
        var style = tokens.GroupStyle;

        // On light themes a file's icon is half the group color, half grey, so that files stand apart from folders;
        // the dark themes and Pastel use one icon color for both.
        var fileIcon = dark || style == GroupStyle.Tinted ? visual.ItemIcon : ColorMath.Mix(g, 0.5, tokens.Muted);

        // Opaque colors, the count corrected to 4.5:1 against its pill as it shows over the header.
        var badge = GroupVisuals.CountBadge(style, visual.HeaderBackground, tokens.Muted);

        var look = new GroupLook
        {
            HeaderBackground = Brush(visual.HeaderBackground),
            HeaderText = Brush(visual.HeaderText),
            Accent = Brush(visual.Accent),
            BodyBackground = Brush(visual.BodyBackground),
            FolderIcon = Brush(visual.ItemIcon),
            FileIcon = Brush(fileIcon),
            ItemHover = Brush(g, 0.15),
            ItemText = Brush(tokens.Text),
            ItemMuted = Brush(tokens.Muted),
            HeaderGlyph = Brush(tokens.Muted),
            CountText = Brush(badge.Text),
            CountBackground = Brush(badge.Background),
            DotRing = Brush(tokens.Card),
            FrameBrush = Brushes.Transparent,
            FrameThickness = default,
            ContentInset = default,
            LeftAccentWidth = 0,
            TopAccentHeight = 0,
            HeaderUnderline = default,
            HeaderPadding = DefaultHeaderPadding,
            TitleFontSize = TitleSize,
            UppercaseTitle = tokens.UppercaseTitles,
            ShadowFill = Brushes.Transparent,
        };

        return style switch
        {
            // Cards: the header filled with the group color, a white title; a soft shadow.
            GroupStyle.SolidHeader => look with
            {
                HeaderPadding = new Thickness(12, 9, 12, 9),
                HeaderGlyph = Brush(White, 0.92),
                DotRing = Brush(White),
                FrameBrush = dark ? Brush(tokens.Border) : Brush(Black, 0.04),
                FrameThickness = new Thickness(1),
                Shadow = dark ? Drop(Black, 6, 16, 0.5) : Drop("#281e0a", 4, 14, 0.10),
                ShadowFill = Brush(tokens.Card),
            },

            // Pastel: the whole block tinted, the header more strongly; no edge, no shadow.
            GroupStyle.Tinted => look,

            // Paper: a thin frame, a 2 px underline in the group color under the title (not while collapsed).
            GroupStyle.Underline => look with
            {
                FrameBrush = Brush(tokens.Border),
                FrameThickness = new Thickness(1),
                ContentInset = new Thickness(1),
                HeaderUnderline = collapsed ? default : new Thickness(0, 0, 0, 2),
                TitleFontSize = 11.5,
            },

            // Outline: no fill of its own, a 1.5 px frame in the group color.
            GroupStyle.Outline => look with
            {
                FrameBrush = Brush(visual.Accent),
                FrameThickness = new Thickness(1.5),
                ContentInset = new Thickness(1.5),
            },

            // Neon: a frame and a glow in the group color; on dark, the title glows too.
            GroupStyle.Glow => look with
            {
                FrameBrush = Brush(visual.Accent, 0.75),
                FrameThickness = new Thickness(1),
                ContentInset = new Thickness(1),
                Shadow = dark ? Drop(visual.Accent, 0, 14, 0.40) : Drop(visual.Accent, 0, 12, 0.35),
                ShadowFill = Brush(tokens.Card),
                TitleEffect = dark ? Drop(visual.Accent, 0, 8, 0.6) : null,
            },

            // Compact: a 2 px bar in the group color on top, a hairline ring, a shadow; smaller capitals.
            GroupStyle.TopBar => look with
            {
                TopAccentHeight = 2,
                ContentInset = new Thickness(0, 2, 0, 0),
                FrameBrush = Brush(tokens.Border),
                FrameThickness = new Thickness(1),
                Shadow = dark ? Drop(Black, 6, 14, 0.55) : Drop(Black, 3, 8, 0.08),
                ShadowFill = Brush(tokens.Card),
                TitleFontSize = 11.85,
            },

            // Fluent: a 4 px edge in the group color on the left, a tinted header, a hairline ring, a light shadow.
            _ => look with
            {
                LeftAccentWidth = 4,
                ContentInset = new Thickness(4, 0, 0, 0),
                FrameBrush = Brush(tokens.Border),
                FrameThickness = new Thickness(1),
                Shadow = dark ? Drop(Black, 2, 8, 0.4) : Drop(Black, 1, 4, 0.08),
                ShadowFill = Brush(tokens.Card),
            },
        };
    }

    private static SolidColorBrush Brush(string hex, double opacity = 1) => ThemeApplier.SolidBrush(hex, opacity);

    /// <summary>
    /// A shadow straight below (<paramref name="offset"/> DIPs), like the CSS <c>box-shadow: 0 offset blur color</c>
    /// of the mockups: WPF's blur radius spans about three standard deviations where CSS's spans two, hence the 1.5.
    /// </summary>
    private static DropShadowEffect Drop(string hex, double offset, double blur, double opacity)
    {
        var effect = new DropShadowEffect
        {
            Color = ThemeApplier.ToColor(hex),
            Direction = 270,
            ShadowDepth = offset,
            BlurRadius = blur * 1.5,
            Opacity = opacity,
            RenderingBias = RenderingBias.Performance,
        };
        effect.Freeze();
        return effect;
    }
}
