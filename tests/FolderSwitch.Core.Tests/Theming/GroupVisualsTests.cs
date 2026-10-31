using FolderSwitch.Core.Theming;

namespace FolderSwitch.Core.Tests.Theming;

public class GroupVisualsTests
{
    private const string White = "#ffffff";
    private const string Black = "#000000";

    [Theory]
    [MemberData(nameof(ThemeTestData.AllThemesAndVariants), MemberType = typeof(ThemeTestData))]
    public void Group_headers_readable_for_every_palette_color(string theme, Variant variant)
    {
        var card = ThemeCatalog.Resolve(theme, variant, null).Card;

        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var visual = GroupVisuals.For(theme, variant, color.Hex, card);

            Assert.True(
                ColorMath.ContrastRatio(visual.HeaderText, visual.HeaderBackground) >= 4.5,
                $"{theme}/{variant}/{color.Key}: {visual.HeaderText} on {visual.HeaderBackground}");
        }
    }

    [Theory]
    [MemberData(nameof(ThemeTestData.AllThemesAndVariants), MemberType = typeof(ThemeTestData))]
    public void Item_text_readable_on_the_group_body_for_every_palette_color(string theme, Variant variant)
    {
        // Item names are drawn on the group body (for Tinted, a tint of the group color).
        var tokens = ThemeCatalog.Resolve(theme, variant, null);

        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var visual = GroupVisuals.For(theme, variant, color.Hex, tokens.Card);

            Assert.True(
                ColorMath.ContrastRatio(tokens.Text, visual.BodyBackground) >= 4.5,
                $"{theme}/{variant}/{color.Key}: {tokens.Text} on {visual.BodyBackground}");
        }
    }

    [Theory]
    [InlineData(Variant.Light)]
    [InlineData(Variant.Dark)]
    public void Group_headers_and_items_readable_with_a_custom_background_on_outline(Variant variant)
    {
        // Outline draws its groups straight on the window background, so the user's color is the card.
        var backgrounds = ThemeCatalog.Backgrounds(Variant.Light)
            .Concat(ThemeCatalog.Backgrounds(Variant.Dark))
            .Select(c => c.Hex)
            .Concat([White, Black, "#808080", "#f59e0b", "#3b82f6"]);

        foreach (var background in backgrounds)
        {
            var tokens = ThemeCatalog.Resolve("outline", variant, background);

            foreach (var color in ThemeCatalog.GroupPalette)
            {
                var visual = GroupVisuals.For("outline", variant, color.Hex, tokens.Card);

                Assert.True(
                    ColorMath.ContrastRatio(visual.HeaderText, visual.HeaderBackground) >= 4.5,
                    $"{variant}/{color.Key} header on {background}");
                Assert.True(
                    ColorMath.ContrastRatio(tokens.Text, visual.BodyBackground) >= 4.5,
                    $"{variant}/{color.Key} item text on {background}");
            }
        }
    }

    [Fact]
    public void LeftAccent_tints_the_header_and_keeps_the_card_as_body()
    {
        const string card = "#ffffff";
        const string darkCard = "#2d2d2d";

        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var light = GroupVisuals.For("fluent", Variant.Light, color.Hex, card);
            Assert.Equal(color.Hex, light.Accent);
            Assert.Equal(card, light.BodyBackground);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.11, White), light.HeaderBackground);
            Assert.Equal(color.Hex, light.ItemIcon);

            var dark = GroupVisuals.For("fluent", Variant.Dark, color.Hex, darkCard);
            Assert.Equal(color.Hex, dark.Accent);
            Assert.Equal(darkCard, dark.BodyBackground);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.22, darkCard), dark.HeaderBackground);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.78, White), dark.ItemIcon);
        }
    }

    [Fact]
    public void LeftAccent_known_values_for_blue()
    {
        var light = GroupVisuals.For("fluent", Variant.Light, "#3b82f6", "#ffffff");

        Assert.Equal("#e9f1fe", light.HeaderBackground);
        Assert.Equal("#3b82f6", light.Accent);
        Assert.Equal("#ffffff", light.BodyBackground);
        Assert.Equal("#3b82f6", light.ItemIcon);
        // 65 % blue over black, already readable on the tinted header.
        Assert.Equal(ColorMath.Mix("#3b82f6", 0.65, Black), light.HeaderText);
    }

    [Fact]
    public void SolidHeader_starts_from_the_group_color_and_keeps_the_card_as_body()
    {
        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var light = GroupVisuals.For("cards", Variant.Light, color.Hex, "#ffffff");
            Assert.Equal(ColorMath.EnsureBackgroundContrast(White, color.Hex, 4.5), light.HeaderBackground);
            Assert.Equal(light.HeaderBackground, light.Accent);
            Assert.Equal("#ffffff", light.BodyBackground);
            Assert.Equal(color.Hex, light.ItemIcon);

            var dark = GroupVisuals.For("cards", Variant.Dark, color.Hex, "#262830");
            Assert.Equal(
                ColorMath.EnsureBackgroundContrast(White, ColorMath.Mix(color.Hex, 0.85, Black), 4.5),
                dark.HeaderBackground);
            Assert.Equal(dark.HeaderBackground, dark.Accent);
            Assert.Equal("#262830", dark.BodyBackground);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.78, White), dark.ItemIcon);
        }
    }

    [Theory]
    [InlineData(Variant.Light, "#ffffff")]
    [InlineData(Variant.Dark, "#262830")]
    public void SolidHeader_title_is_always_white_on_a_header_darkened_enough(Variant variant, string card)
    {
        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var visual = GroupVisuals.For("cards", variant, color.Hex, card);

            Assert.Equal(White, visual.HeaderText);
            Assert.True(
                ColorMath.ContrastRatio(White, visual.HeaderBackground) >= 4.5,
                $"{variant}/{color.Key}: white on {visual.HeaderBackground}");
        }
    }

    [Fact]
    public void SolidHeader_leaves_a_header_that_already_carries_white_untouched()
    {
        // Slate light (#64748b) already reaches 4.5:1 with white; the dark mix of blue does too.
        Assert.Equal("#64748b", GroupVisuals.For("cards", Variant.Light, "#64748b", "#ffffff").HeaderBackground);
        Assert.Equal(
            ColorMath.Mix("#3b82f6", 0.85, Black),
            GroupVisuals.For("cards", Variant.Dark, "#3b82f6", "#262830").HeaderBackground);
    }

    [Fact]
    public void SolidHeader_only_darkens_the_group_color_to_keep_its_hue()
    {
        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var header = GroupVisuals.For("cards", Variant.Light, color.Hex, "#ffffff").HeaderBackground;

            // Every channel is at most the original one: the header is a blend of the color with black.
            for (var i = 1; i <= 5; i += 2)
            {
                Assert.True(
                    Convert.ToInt32(header.Substring(i, 2), 16) <= Convert.ToInt32(color.Hex.Substring(i, 2), 16),
                    $"{color.Key}: {header} is not a darkening of {color.Hex}");
            }
        }

        // Blue only reaches 3.68:1 with white: it is darkened, by the smallest 5 % step that is enough.
        var blue = GroupVisuals.For("cards", Variant.Light, "#3b82f6", "#ffffff").HeaderBackground;
        var expected = Enumerable.Range(1, 20)
            .Select(step => ColorMath.Mix(Black, step / 20.0, "#3b82f6"))
            .First(candidate => ColorMath.ContrastRatio(White, candidate) >= 4.5);
        Assert.NotEqual("#3b82f6", blue);
        Assert.Equal(expected, blue);
    }

    [Fact]
    public void Tinted_mixes_the_group_color_into_the_body_and_header()
    {
        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var light = GroupVisuals.For("pastel", Variant.Light, color.Hex, "#ffffff");
            Assert.Equal(ColorMath.Mix(color.Hex, 0.08, White), light.BodyBackground);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.20, White), light.HeaderBackground);
            Assert.Equal(light.HeaderBackground, light.Accent);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.8, Black), light.ItemIcon);

            var dark = GroupVisuals.For("pastel", Variant.Dark, color.Hex, "#1f2026");
            Assert.Equal(ColorMath.Mix(color.Hex, 0.12, "#1f2026"), dark.BodyBackground);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.28, "#1f2026"), dark.HeaderBackground);
            Assert.Equal(dark.HeaderBackground, dark.Accent);
            Assert.Equal(ColorMath.Mix(color.Hex, 0.78, White), dark.ItemIcon);
        }
    }

    [Theory]
    [InlineData(Variant.Light, "#ffffff", "#1c1c22")]
    [InlineData(Variant.Dark, "#1d1e22", "#f1f1f5")]
    public void Underline_keeps_the_card_surface_and_the_base_text_for_the_title(
        Variant variant, string card, string baseText)
    {
        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var visual = GroupVisuals.For("paper", variant, color.Hex, card);

            Assert.Equal(card, visual.HeaderBackground);
            Assert.Equal(card, visual.BodyBackground);
            Assert.Equal(color.Hex, visual.Accent);
            Assert.Equal(baseText, visual.HeaderText);
        }
    }

    [Theory]
    [InlineData("outline", Variant.Light, "#fafafa", 0.75)]
    [InlineData("outline", Variant.Dark, "#121317", 0.55)]
    [InlineData("neon", Variant.Light, "#ffffff", 0.8)]
    [InlineData("neon", Variant.Dark, "#15161c", 0.65)]
    [InlineData("compact", Variant.Light, "#ffffff", 0.7)]
    [InlineData("compact", Variant.Dark, "#2b2d37", 0.55)]
    public void Outline_Glow_and_TopBar_use_the_card_and_a_colored_accent(
        string theme, Variant variant, string card, double titleWeight)
    {
        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var visual = GroupVisuals.For(theme, variant, color.Hex, card);

            Assert.Equal(card, visual.HeaderBackground);
            Assert.Equal(card, visual.BodyBackground);
            Assert.Equal(color.Hex, visual.Accent);
            var title = variant == Variant.Light
                ? ColorMath.Mix(color.Hex, titleWeight, Black)
                : ColorMath.Mix(color.Hex, titleWeight, White);
            Assert.Equal(ColorMath.EnsureContrast(title, card, 4.5), visual.HeaderText);
            Assert.Equal(
                variant == Variant.Light ? color.Hex : ColorMath.Mix(color.Hex, 0.78, White),
                visual.ItemIcon);
        }
    }

    [Fact]
    public void Outputs_are_normalized_lowercase_rgb()
    {
        var visual = GroupVisuals.For("fluent", Variant.Light, "#FF3B82F6", "#FFFFFFFF");

        Assert.Equal("#3b82f6", visual.Accent);
        Assert.Equal("#ffffff", visual.BodyBackground);
        Assert.Matches("^#[0-9a-f]{6}$", visual.HeaderBackground);
        Assert.Matches("^#[0-9a-f]{6}$", visual.HeaderText);
        Assert.Matches("^#[0-9a-f]{6}$", visual.ItemIcon);
    }

    [Fact]
    public void An_unknown_theme_falls_back_to_the_fluent_style()
    {
        Assert.Equal(
            GroupVisuals.For("fluent", Variant.Dark, "#3b82f6", "#2d2d2d"),
            GroupVisuals.For("nope", Variant.Dark, "#3b82f6", "#2d2d2d"));
    }

    [Fact]
    public void An_invalid_color_is_rejected()
    {
        Assert.Throws<FormatException>(() => GroupVisuals.For("fluent", Variant.Light, "blue", "#ffffff"));
        Assert.Throws<FormatException>(() => GroupVisuals.For("fluent", Variant.Light, "#3b82f6", "white"));
    }

    // ----- Count badge -----

    [Theory]
    [MemberData(nameof(ThemeTestData.AllThemesAndVariants), MemberType = typeof(ThemeTestData))]
    public void Count_badge_readable_on_its_pill_for_every_palette_color(string theme, Variant variant)
    {
        var tokens = ThemeCatalog.Resolve(theme, variant, null);

        foreach (var color in ThemeCatalog.GroupPalette)
        {
            AssertReadableBadge(tokens, theme, variant, color.Hex, $"{theme}/{variant}/{color.Key}");
        }
    }

    [Theory]
    [MemberData(nameof(ThemeTestData.AllThemesAndVariants), MemberType = typeof(ThemeTestData))]
    public void Count_badge_readable_with_every_offered_and_odd_background(string theme, Variant variant)
    {
        // A background only reaches the headers of Outline (drawn on the window), but every theme is checked.
        foreach (var background in OddBackgrounds())
        {
            var tokens = ThemeCatalog.Resolve(theme, variant, background);

            foreach (var color in ThemeCatalog.GroupPalette)
            {
                AssertReadableBadge(tokens, theme, variant, color.Hex, $"{theme}/{variant}/{color.Key} on {background}");
            }
        }
    }

    [Theory]
    [InlineData(Variant.Light)]
    [InlineData(Variant.Dark)]
    public void Count_badge_on_a_filled_header_stays_white_on_a_shaded_pill(Variant variant)
    {
        var tokens = ThemeCatalog.Resolve("cards", variant, null);

        foreach (var color in ThemeCatalog.GroupPalette)
        {
            var visual = GroupVisuals.For("cards", variant, color.Hex, tokens.Card);
            var badge = GroupVisuals.CountBadge(GroupStyle.SolidHeader, visual.HeaderBackground, tokens.Muted);

            Assert.Equal(White, badge.Text);
            Assert.Equal(ColorMath.Mix(Black, 0.14, visual.HeaderBackground), badge.Background);
        }
    }

    [Fact]
    public void Count_badge_pill_is_the_muted_color_composited_over_the_header()
    {
        // A lightly tinted header, the light muted color: 14 % of it over the header.
        var badge = GroupVisuals.CountBadge(GroupStyle.LeftAccent, "#e9f1fe", "#74747f");

        Assert.Equal(ColorMath.Mix("#74747f", 0.14, "#e9f1fe"), badge.Background);
        Assert.Matches("^#[0-9a-f]{6}$", badge.Text);
        Assert.True(ColorMath.ContrastRatio(badge.Text, badge.Background) >= 4.5);
    }

    [Fact]
    public void Count_badge_keeps_the_muted_color_when_it_already_reads()
    {
        // Black-ish muted text on a white header: nothing to correct.
        var badge = GroupVisuals.CountBadge(GroupStyle.Underline, "#ffffff", "#333333");

        Assert.Equal("#333333", badge.Text);
    }

    private static IEnumerable<string> OddBackgrounds() =>
        ThemeCatalog.Backgrounds(Variant.Light)
            .Concat(ThemeCatalog.Backgrounds(Variant.Dark))
            .Select(c => c.Hex)
            .Concat([White, Black, "#808080", "#f59e0b", "#3b82f6", "#ef4444", "#22c55e"]);

    private static void AssertReadableBadge(ThemeTokens tokens, string theme, Variant variant, string groupHex, string what)
    {
        var visual = GroupVisuals.For(theme, variant, groupHex, tokens.Card);
        var badge = GroupVisuals.CountBadge(tokens.GroupStyle, visual.HeaderBackground, tokens.Muted);

        Assert.Matches("^#[0-9a-f]{6}$", badge.Background);
        Assert.Matches("^#[0-9a-f]{6}$", badge.Text);
        Assert.True(
            ColorMath.ContrastRatio(badge.Text, badge.Background) >= 4.5,
            $"{what}: {badge.Text} on {badge.Background} ({ColorMath.ContrastRatio(badge.Text, badge.Background):F2}:1)");
    }
}
