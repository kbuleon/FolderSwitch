using FolderSwitch.Core.Model;
using FolderSwitch.Core.Theming;

namespace FolderSwitch.Core.Tests.Theming;

public class ThemeCatalogTests
{
    private static readonly Variant[] BothVariants = [Variant.Light, Variant.Dark];

    [Fact]
    public void ThemeKeys_are_the_seven_themes_in_spec_order()
    {
        Assert.Equal(
            new[] { "fluent", "cards", "pastel", "paper", "outline", "neon", "compact" },
            ThemeCatalog.ThemeKeys);
    }

    [Theory]
    // theme, variant, background, card, border, radius, style, dense, uppercase
    [InlineData("fluent", Variant.Light, "#f7f5f0", "#ffffff", "#1a000000", 10, GroupStyle.LeftAccent, false, false)]
    [InlineData("fluent", Variant.Dark, "#1c1c1c", "#2d2d2d", "#1cffffff", 10, GroupStyle.LeftAccent, false, false)]
    [InlineData("cards", Variant.Light, "#f7f5f0", "#ffffff", "#e3ded4", 14, GroupStyle.SolidHeader, false, false)]
    [InlineData("cards", Variant.Dark, "#16171c", "#262830", "#1cffffff", 14, GroupStyle.SolidHeader, false, false)]
    [InlineData("pastel", Variant.Light, "#ffffff", "#ffffff", "#1a000000", 16, GroupStyle.Tinted, false, false)]
    [InlineData("pastel", Variant.Dark, "#15161a", "#1f2026", "#1cffffff", 16, GroupStyle.Tinted, false, false)]
    [InlineData("paper", Variant.Light, "#ffffff", "#ffffff", "#e2e2e7", 4, GroupStyle.Underline, false, true)]
    [InlineData("paper", Variant.Dark, "#141518", "#1d1e22", "#34353c", 4, GroupStyle.Underline, false, true)]
    [InlineData("outline", Variant.Light, "#fafafa", "#fafafa", "#1a000000", 12, GroupStyle.Outline, false, false)]
    [InlineData("outline", Variant.Dark, "#121317", "#121317", "#1cffffff", 12, GroupStyle.Outline, false, false)]
    [InlineData("neon", Variant.Light, "#f4f5f8", "#ffffff", "#1a000000", 10, GroupStyle.Glow, false, false)]
    [InlineData("neon", Variant.Dark, "#0e0f13", "#15161c", "#1cffffff", 10, GroupStyle.Glow, false, false)]
    [InlineData("compact", Variant.Light, "#eef0f3", "#ffffff", "#d9dce3", 7, GroupStyle.TopBar, true, true)]
    [InlineData("compact", Variant.Dark, "#0e0f13", "#2b2d37", "#424654", 7, GroupStyle.TopBar, true, true)]
    public void Get_matches_the_theme_table(
        string theme, Variant variant, string background, string card, string border,
        int radius, GroupStyle style, bool dense, bool uppercase)
    {
        var tokens = ThemeCatalog.Get(theme, variant);

        Assert.Equal(background, tokens.Background);
        Assert.Equal(card, tokens.Card);
        Assert.Equal(border, tokens.Border);
        Assert.Equal(radius, tokens.DefaultRadius);
        Assert.Equal(style, tokens.GroupStyle);
        Assert.Equal(dense, tokens.Dense);
        Assert.Equal(uppercase, tokens.UppercaseTitles);
    }

    [Theory]
    [InlineData(Variant.Light, "#1c1c22", "#74747f")]
    [InlineData(Variant.Dark, "#f1f1f5", "#a2a4b0")]
    public void Get_uses_the_base_text_colors_and_defaults_the_on_background_pair_to_them(
        Variant variant, string text, string muted)
    {
        foreach (var theme in ThemeCatalog.ThemeKeys)
        {
            var tokens = ThemeCatalog.Get(theme, variant);

            Assert.Equal(text, tokens.Text);
            Assert.Equal(muted, tokens.Muted);
            Assert.Equal(text, tokens.OnBackgroundText);
            Assert.Equal(muted, tokens.OnBackgroundMuted);
        }
    }

    [Fact]
    public void Get_falls_back_to_fluent_for_an_unknown_theme()
    {
        Assert.Equal(ThemeCatalog.Get("fluent", Variant.Dark), ThemeCatalog.Get("nope", Variant.Dark));
        Assert.Equal(ThemeCatalog.Get("fluent", Variant.Light), ThemeCatalog.Get("", Variant.Light));
    }

    [Theory]
    [MemberData(nameof(ThemeTestData.AllThemesAndVariants), MemberType = typeof(ThemeTestData))]
    public void All_themes_text_readable(string theme, Variant variant)
    {
        var tokens = ThemeCatalog.Resolve(theme, variant, null);

        Assert.True(ColorMath.ContrastRatio(tokens.Text, tokens.Card) >= 4.5, "Text on card");
        Assert.True(ColorMath.ContrastRatio(tokens.Text, tokens.Background) >= 4.5, "Text on background");
        Assert.True(ColorMath.ContrastRatio(tokens.Muted, tokens.Card) >= 3.0, "Muted on card");
        Assert.True(ColorMath.ContrastRatio(tokens.OnBackgroundText, tokens.Background) >= 4.5, "On-background text");
        Assert.True(ColorMath.ContrastRatio(tokens.OnBackgroundMuted, tokens.Background) >= 3.0, "On-background muted");
    }

    [Theory]
    [MemberData(nameof(ThemeTestData.AllThemesAndVariants), MemberType = typeof(ThemeTestData))]
    public void Get_tokens_are_readable_before_any_override(string theme, Variant variant)
    {
        var tokens = ThemeCatalog.Get(theme, variant);

        Assert.True(ColorMath.ContrastRatio(tokens.Text, tokens.Card) >= 4.5);
        Assert.True(ColorMath.ContrastRatio(tokens.Text, tokens.Background) >= 4.5);
        Assert.True(ColorMath.ContrastRatio(tokens.Muted, tokens.Card) >= 3.0);
    }

    [Fact]
    public void Custom_background_keeps_text_readable()
    {
        var onWhite = ThemeCatalog.Resolve("fluent", Variant.Dark, "#ffffff");
        var onNavy = ThemeCatalog.Resolve("fluent", Variant.Light, "#121a2b");

        Assert.Equal("#ffffff", onWhite.Background);
        Assert.True(ColorMath.ContrastRatio(onWhite.OnBackgroundText, "#ffffff") >= 4.5);
        Assert.True(ColorMath.ContrastRatio(onWhite.OnBackgroundMuted, "#ffffff") >= 3.0);

        Assert.Equal("#121a2b", onNavy.Background);
        Assert.True(ColorMath.ContrastRatio(onNavy.OnBackgroundText, "#121a2b") >= 4.5);
        Assert.True(ColorMath.ContrastRatio(onNavy.OnBackgroundMuted, "#121a2b") >= 3.0);
    }

    [Fact]
    public void Resolve_leaves_the_card_text_alone_on_a_non_outline_theme()
    {
        var plain = ThemeCatalog.Get("fluent", Variant.Dark);
        var resolved = ThemeCatalog.Resolve("fluent", Variant.Dark, "#ffffff");

        // Cards keep their own surface, so the text drawn on them is unchanged...
        Assert.Equal(plain.Card, resolved.Card);
        Assert.Equal(plain.Text, resolved.Text);
        Assert.Equal(plain.Muted, resolved.Muted);
        // ...while the text drawn straight on the window background adapts.
        Assert.NotEqual(plain.Text, resolved.OnBackgroundText);
        Assert.NotEqual(plain.Muted, resolved.OnBackgroundMuted);
    }

    [Theory]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#121a2b")]
    [InlineData("#808080")]
    [InlineData("#f59e0b")]
    [InlineData("#3b82f6")]
    public void Resolve_keeps_on_background_text_readable_on_any_override(string background)
    {
        foreach (var theme in ThemeCatalog.ThemeKeys)
        {
            foreach (var variant in BothVariants)
            {
                var tokens = ThemeCatalog.Resolve(theme, variant, background);

                Assert.Equal(background, tokens.Background);
                Assert.True(
                    ColorMath.ContrastRatio(tokens.OnBackgroundText, background) >= 4.5,
                    $"{theme}/{variant} text on {background}");
                Assert.True(
                    ColorMath.ContrastRatio(tokens.OnBackgroundMuted, background) >= 3.0,
                    $"{theme}/{variant} muted on {background}");
            }
        }
    }

    [Theory]
    [InlineData(Variant.Light, "#ffffff")]
    [InlineData(Variant.Light, "#121a2b")]
    [InlineData(Variant.Dark, "#ffffff")]
    [InlineData(Variant.Dark, "#eaf1fb")]
    public void Outline_card_follows_the_background_and_its_text_stays_readable(Variant variant, string background)
    {
        var tokens = ThemeCatalog.Resolve("outline", variant, background);

        Assert.Equal(background, tokens.Background);
        Assert.Equal(background, tokens.Card);
        Assert.Equal(tokens.OnBackgroundText, tokens.Text);
        Assert.Equal(tokens.OnBackgroundMuted, tokens.Muted);
        Assert.True(ColorMath.ContrastRatio(tokens.Text, tokens.Card) >= 4.5);
        Assert.True(ColorMath.ContrastRatio(tokens.Muted, tokens.Card) >= 3.0);
    }

    [Fact]
    public void Outline_without_override_uses_its_background_as_card()
    {
        Assert.Equal("#121317", ThemeCatalog.Resolve("outline", Variant.Dark, null).Card);
        Assert.Equal("#fafafa", ThemeCatalog.Resolve("outline", Variant.Light, null).Card);
    }

    [Fact]
    public void Resolve_without_override_matches_get()
    {
        foreach (var theme in ThemeCatalog.ThemeKeys)
        {
            foreach (var variant in BothVariants)
            {
                Assert.Equal(ThemeCatalog.Get(theme, variant), ThemeCatalog.Resolve(theme, variant, null));
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a color")]
    [InlineData("#12345")]
    public void Resolve_ignores_an_invalid_override(string bad)
    {
        // A hand-edited config must never crash the theme.
        Assert.Equal(ThemeCatalog.Get("fluent", Variant.Dark), ThemeCatalog.Resolve("fluent", Variant.Dark, bad));
    }

    [Fact]
    public void Resolve_normalizes_the_override()
    {
        Assert.Equal("#121a2b", ThemeCatalog.Resolve("fluent", Variant.Dark, "#FF121A2B").Background);
        Assert.Equal("#eaf1fb", ThemeCatalog.Resolve("fluent", Variant.Light, "#EAF1FB").Background);
    }

    [Fact]
    public void Backgrounds_lists_match_spec()
    {
        Assert.Equal(
            new[]
            {
                ("white", "#ffffff"), ("offWhite", "#fafafa"), ("cream", "#f7f5f0"), ("pearl", "#eef0f3"),
                ("glacier", "#eaf1fb"), ("mint", "#eaf5ef"), ("blush", "#f9eef0"),
            },
            ThemeCatalog.Backgrounds(Variant.Light).Select(c => (c.Key, c.Hex)));

        Assert.Equal(
            new[]
            {
                ("black", "#0e0f13"), ("charcoal", "#16171c"), ("anthracite", "#1c1c1c"), ("graphite", "#24262c"),
                ("slate", "#1b2130"), ("midnight", "#121a2b"), ("forest", "#13201a"), ("aubergine", "#1e1626"),
            },
            ThemeCatalog.Backgrounds(Variant.Dark).Select(c => (c.Key, c.Hex)));

        Assert.Equal(7, ThemeCatalog.Backgrounds(Variant.Light).Count);
        Assert.Equal(8, ThemeCatalog.Backgrounds(Variant.Dark).Count);
    }

    [Fact]
    public void GroupPalette_has_the_ten_colors_in_spec_order()
    {
        Assert.Equal(
            new[]
            {
                ("blue", "#3b82f6"), ("cyan", "#06b6d4"), ("green", "#22c55e"), ("olive", "#84cc16"),
                ("amber", "#f59e0b"), ("orange", "#f97316"), ("red", "#ef4444"), ("pink", "#ec4899"),
                ("violet", "#8b5cf6"), ("slate", "#64748b"),
            },
            ThemeCatalog.GroupPalette.Select(c => (c.Key, c.Hex)));
    }

    [Fact]
    public void Catalog_lists_cannot_be_mutated_through_a_cast()
    {
        Assert.False(ThemeCatalog.GroupPalette is NamedColor[]);
        Assert.False(ThemeCatalog.Backgrounds(Variant.Light) is NamedColor[]);
        Assert.False(ThemeCatalog.ThemeKeys is string[]);
    }

    [Theory]
    [InlineData(GroupRadius.Theme, "paper", 4)]
    [InlineData(GroupRadius.Strong, "paper", 18)]
    [InlineData(GroupRadius.Square, "pastel", 0)]
    [InlineData(GroupRadius.Slight, "pastel", 4)]
    [InlineData(GroupRadius.Medium, "pastel", 10)]
    [InlineData(GroupRadius.Theme, "fluent", 10)]
    [InlineData(GroupRadius.Theme, "cards", 14)]
    [InlineData(GroupRadius.Theme, "pastel", 16)]
    [InlineData(GroupRadius.Theme, "outline", 12)]
    [InlineData(GroupRadius.Theme, "neon", 10)]
    [InlineData(GroupRadius.Theme, "compact", 7)]
    public void RadiusPx_resolves_the_theme_default_or_the_fixed_value(GroupRadius radius, string theme, int expected)
    {
        Assert.Equal(expected, ThemeCatalog.RadiusPx(radius, theme));
    }
}
