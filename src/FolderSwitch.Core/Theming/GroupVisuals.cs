namespace FolderSwitch.Core.Theming;

/// <summary>Computes the colors of a group block from the theme's group style and the group's color.</summary>
public static class GroupVisuals
{
    private const string White = "#ffffff";
    private const string Black = "#000000";
    private const string PastelDarkSurface = "#1f2026";
    private const double TitleMinRatio = 4.5;

    /// <summary>
    /// Below this contrast of white on a filled header's color (yellow, amber, green…), the header keeps its color
    /// and takes a dark title: darkening it until white reads would turn it brown.
    /// </summary>
    private const double WhiteTitleMinRatio = 3.0;

    /// <summary>Share of the badge color in its pill, over the header (the mockups' 14 %).</summary>
    private const double BadgeTint = 0.14;

    /// <summary>
    /// Colors of a group drawn in <paramref name="theme"/> / <paramref name="v"/>. Every title is
    /// corrected with <see cref="ColorMath.EnsureContrast"/> so it keeps at least 4.5:1 against its
    /// header, except the white Cards title, which darkens its header instead
    /// (<see cref="ColorMath.EnsureBackgroundContrast"/>); a Cards header too light for white keeps its
    /// color and takes a dark title. Other non-text colors (accent, icon, fills) are not
    /// contrast-checked. An unknown theme key behaves like <c>fluent</c>.
    /// </summary>
    /// <param name="groupHex">The group's color.</param>
    /// <param name="cardHex">The theme's card surface (<see cref="ThemeTokens.Card"/> after
    /// <see cref="ThemeCatalog.Resolve"/>).</param>
    /// <exception cref="FormatException">A color is not <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</exception>
    public static GroupVisual For(string theme, Variant v, string groupHex, string cardHex)
    {
        var style = ThemeCatalog.Get(theme, v).GroupStyle;
        var g = ColorMath.Normalize(groupHex);
        var card = ColorMath.Normalize(cardHex);
        var dark = v == Variant.Dark;

        // Folder icon: the group color on light, lifted toward white on dark. Shared by most styles.
        var icon = dark ? ColorMath.Mix(g, 0.78, White) : g;

        switch (style)
        {
            case GroupStyle.SolidHeader:
            {
                // Colored header. A light color keeps its fill under a dark title; on any other, the title is
                // white and, when white does not read at 4.5:1 yet, the header is darkened (same hue), never the
                // title.
                var fill = dark ? ColorMath.Mix(g, 0.85, Black) : g;
                if (!TakesWhiteTitle(fill))
                {
                    return Build(fill, ThemeCatalog.BaseText(Variant.Light), fill, card, icon);
                }

                var header = ColorMath.EnsureBackgroundContrast(White, fill, TitleMinRatio);
                return Build(header, White, header, card, icon);
            }

            case GroupStyle.Tinted:
            {
                var body = dark ? ColorMath.Mix(g, 0.12, PastelDarkSurface) : ColorMath.Mix(g, 0.08, White);
                var header = dark ? ColorMath.Mix(g, 0.28, PastelDarkSurface) : ColorMath.Mix(g, 0.20, White);
                var title = dark ? ColorMath.Mix(g, 0.4, White) : ColorMath.Mix(g, 0.6, Black);
                var tintedIcon = dark ? ColorMath.Mix(g, 0.78, White) : ColorMath.Mix(g, 0.8, Black);
                return Build(header, title, header, body, tintedIcon);
            }

            case GroupStyle.Underline:
                return Build(card, ThemeCatalog.BaseText(v), g, card, icon);

            case GroupStyle.Outline:
                return Build(card, Title(g, dark, lightWeight: 0.75, darkWeight: 0.55), g, card, icon);

            case GroupStyle.Glow:
                return Build(card, Title(g, dark, lightWeight: 0.8, darkWeight: 0.65), g, card, icon);

            case GroupStyle.TopBar:
                return Build(card, Title(g, dark, lightWeight: 0.7, darkWeight: 0.55), g, card, icon);

            default: // LeftAccent
            {
                var header = dark ? ColorMath.Mix(g, 0.22, card) : ColorMath.Mix(g, 0.11, White);
                return Build(header, Title(g, dark, lightWeight: 0.65, darkWeight: 0.45), g, card, icon);
            }
        }
    }

    /// <summary>
    /// Colors of the item-count badge drawn on a header of <paramref name="headerHex"/> (the
    /// <see cref="GroupVisual.HeaderBackground"/> of the group). The pill is a 14 % tint of the theme's
    /// muted color composited over the header, and the count starts from that muted color, corrected
    /// with <see cref="ColorMath.EnsureContrast"/> to 4.5:1 against the pill. On a filled header
    /// (<see cref="GroupStyle.SolidHeader"/>) the count is in the title's color, white or dark, and the
    /// pill is a 14 % shade instead: a lighter pill would take white under the 4.5:1 the header was
    /// darkened for.
    /// </summary>
    /// <param name="style">The theme's group style.</param>
    /// <param name="headerHex">The header fill.</param>
    /// <param name="mutedHex">The theme's muted text color (<see cref="ThemeTokens.Muted"/>).</param>
    /// <exception cref="FormatException">A color is not <c>#RRGGBB</c> or <c>#AARRGGBB</c>.</exception>
    public static CountBadgeVisual CountBadge(GroupStyle style, string headerHex, string mutedHex)
    {
        var header = ColorMath.Normalize(headerHex);

        if (style == GroupStyle.SolidHeader)
        {
            // A header with a white title reads white at 4.5:1, one with a dark title not even at 3:1.
            var title = TakesWhiteTitle(header) ? White : ThemeCatalog.BaseText(Variant.Light);
            var shaded = ColorMath.Mix(Black, BadgeTint, header);
            return new CountBadgeVisual(shaded, ColorMath.EnsureContrast(title, shaded, TitleMinRatio));
        }

        var muted = ColorMath.Normalize(mutedHex);
        var pill = ColorMath.Mix(muted, BadgeTint, header);
        return new CountBadgeVisual(pill, ColorMath.EnsureContrast(muted, pill, TitleMinRatio));
    }

    /// <summary>Whether a filled header of <paramref name="fill"/> takes a white title (else a dark one).</summary>
    private static bool TakesWhiteTitle(string fill) => ColorMath.ContrastRatio(White, fill) >= WhiteTitleMinRatio;

    /// <summary>Group color darkened (light variant) or lightened (dark variant) to read as a title.</summary>
    private static string Title(string g, bool dark, double lightWeight, double darkWeight) =>
        dark ? ColorMath.Mix(g, darkWeight, White) : ColorMath.Mix(g, lightWeight, Black);

    private static GroupVisual Build(string header, string title, string accent, string body, string icon) =>
        new(
            HeaderBackground: header,
            HeaderText: ColorMath.EnsureContrast(title, header, TitleMinRatio),
            Accent: accent,
            BodyBackground: body,
            ItemIcon: icon);
}
