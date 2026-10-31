using System.Windows;
using System.Windows.Media;
using FolderSwitch.App.Themes;
using FolderSwitch.Core.Model;
using FolderSwitch.Core.Theming;

namespace FolderSwitch.App.Views;

/// <summary>
/// The thumbnail of a theme in the appearance menu and the wizard: the window background with two small
/// groups drawn the way the theme draws them (colored edge, solid or tinted header, underline, outline, glow, top
/// bar), in the variant shown and with the corner radius chosen. The colors come from <see cref="ThemeCatalog"/> and
/// <see cref="GroupVisuals"/>, as for the real groups; the two sample groups take colors of the group palette.
/// </summary>
public sealed class ThemePreview : FrameworkElement
{
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme), typeof(string), typeof(ThemePreview),
        new FrameworkPropertyMetadata("fluent", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(Variant), typeof(ThemePreview),
        new FrameworkPropertyMetadata(Variant.Light, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(GroupRadius), typeof(ThemePreview),
        new FrameworkPropertyMetadata(GroupRadius.Theme, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundColorProperty = DependencyProperty.Register(
        nameof(BackgroundColor), typeof(string), typeof(ThemePreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>The ring around the thumbnail: <see cref="ThemeKeys.SwatchRing"/>, a grey that shows on any background.</summary>
    public static readonly DependencyProperty RingBrushProperty = DependencyProperty.Register(
        nameof(RingBrush), typeof(Brush), typeof(ThemePreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double CardTop = 5;

    /// <summary>The thumbnail's corners are this much rounder than nothing, and its groups' this much less round than the real ones.</summary>
    private const double OuterRadius = 5;
    private const double RadiusScale = 2.8;

    /// <summary>The swatch of the custom background: the hues of the group palette around a circle, as the mockups' conic gradient.</summary>
    public static Brush CustomColorBrush { get; } = BuildCustomColorBrush();

    public ThemePreview()
    {
        SetResourceReference(RingBrushProperty, ThemeKeys.SwatchRing);
    }

    /// <summary>The theme drawn (a key of <see cref="ThemeCatalog.ThemeKeys"/>).</summary>
    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public Variant Variant
    {
        get => (Variant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public GroupRadius Radius
    {
        get => (GroupRadius)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>The window background, <c>#rrggbb</c>; <c>null</c> for the theme's own.</summary>
    public string? BackgroundColor
    {
        get => (string?)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public Brush? RingBrush
    {
        get => (Brush?)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = RenderSize.Width;
        var height = RenderSize.Height;
        if (width < 8 || height < 8)
        {
            return;
        }

        var tokens = ThemeCatalog.Resolve(Theme, Variant, BackgroundColor);
        var outer = new Rect(0.5, 0.5, width - 1, height - 1);
        drawingContext.DrawRoundedRectangle(Brush(tokens.Background), RingBrush is { } ring ? new Pen(ring, 1) : null, outer, OuterRadius, OuterRadius);

        var style = tokens.GroupStyle;
        var radius = ThemeCatalog.RadiusPx(Radius, Theme) / RadiusScale;
        var tall = style is GroupStyle.SolidHeader or GroupStyle.Tinted;
        var cardHeight = Math.Min(height - CardTop - 3, tall ? 19 : 17);
        var colors = SampleColors(style);

        for (var i = 0; i < 2; i++)
        {
            var card = new Rect(width * (i == 0 ? 0.08 : 0.54), CardTop, width * 0.38, cardHeight);
            var visual = GroupVisuals.For(Theme, Variant, colors[i], tokens.Card);
            DrawCard(drawingContext, style, card, radius, tokens, visual);
        }
    }

    private static void DrawCard(DrawingContext dc, GroupStyle style, Rect card, double radius, ThemeTokens tokens, GroupVisual visual)
    {
        var shape = new RectangleGeometry(card, radius, radius);
        switch (style)
        {
            case GroupStyle.SolidHeader:
                dc.DrawGeometry(Brush(tokens.Card), null, shape);
                Bar(dc, shape, new Rect(card.X, card.Y, card.Width, 7), visual.HeaderBackground);
                break;

            case GroupStyle.Tinted:
                dc.DrawGeometry(Brush(visual.BodyBackground), null, shape);
                Bar(dc, shape, new Rect(card.X, card.Y, card.Width, 7), visual.HeaderBackground);
                break;

            case GroupStyle.Underline:
                dc.DrawGeometry(Brush(tokens.Card), new Pen(Brush(tokens.Border), 1), shape);
                Bar(dc, shape, new Rect(card.X, card.Y + 5, card.Width, 2), visual.Accent);
                break;

            case GroupStyle.Outline:
                dc.DrawGeometry(null, new Pen(Brush(visual.Accent), 1.5), shape);
                break;

            case GroupStyle.Glow:
                for (var spread = 3; spread >= 1; spread--)
                {
                    var halo = new RectangleGeometry(Rect.Inflate(card, spread * 1.5, spread * 1.5), radius + (spread * 1.5), radius + (spread * 1.5));
                    dc.DrawGeometry(Brush(visual.Accent, 0.09), null, halo);
                }

                dc.DrawGeometry(Brush(tokens.Card), new Pen(Brush(visual.Accent), 1.5), shape);
                break;

            case GroupStyle.TopBar:
                dc.DrawGeometry(Brush(tokens.Card), null, shape);
                Bar(dc, shape, new Rect(card.X, card.Y, card.Width, 4), visual.Accent);
                break;

            default:
                dc.DrawGeometry(Brush(tokens.Card), null, shape);
                Bar(dc, shape, new Rect(card.X, card.Y, 3, card.Height), visual.Accent);
                break;
        }
    }

    /// <summary>A colored edge or header, clipped to the card's rounded corners.</summary>
    private static void Bar(DrawingContext dc, Geometry card, Rect bar, string hex)
    {
        dc.PushClip(card);
        dc.DrawRectangle(Brush(hex), null, bar);
        dc.Pop();
    }

    /// <summary>The sample groups' colors, as in the mockups: blue and amber; cyan and pink for Neon; blue and violet for Compact.</summary>
    private static string[] SampleColors(GroupStyle style) => style switch
    {
        GroupStyle.Glow => [PaletteHex("cyan"), PaletteHex("pink")],
        GroupStyle.TopBar => [PaletteHex("blue"), PaletteHex("violet")],
        _ => [PaletteHex("blue"), PaletteHex("amber")],
    };

    private static string PaletteHex(string key) =>
        ThemeCatalog.GroupPalette.FirstOrDefault(color => color.Key == key)?.Hex ?? ThemeCatalog.GroupPalette[0].Hex;

    private static SolidColorBrush Brush(string hex, double opacity = 1) => ThemeApplier.SolidBrush(hex, opacity);

    private static Brush BuildCustomColorBrush()
    {
        string[] hues = ["red", "amber", "green", "cyan", "blue", "violet", "pink"];
        var center = new Point(0.5, 0.5);
        var group = new DrawingGroup();
        for (var i = 0; i < hues.Length; i++)
        {
            var from = 2 * Math.PI * i / hues.Length;
            var to = 2 * Math.PI * (i + 1) / hues.Length;
            var slice = new StreamGeometry();
            using (var context = slice.Open())
            {
                context.BeginFigure(center, isFilled: true, isClosed: true);
                context.LineTo(new Point(0.5 + (0.5 * Math.Sin(from)), 0.5 - (0.5 * Math.Cos(from))), isStroked: false, isSmoothJoin: false);
                context.ArcTo(
                    new Point(0.5 + (0.5 * Math.Sin(to)), 0.5 - (0.5 * Math.Cos(to))),
                    new Size(0.5, 0.5),
                    0,
                    isLargeArc: false,
                    SweepDirection.Clockwise,
                    isStroked: false,
                    isSmoothJoin: false);
            }

            slice.Freeze();
            group.Children.Add(new GeometryDrawing(Brush(PaletteHex(hues[i])), null, slice));
        }

        var brush = new DrawingBrush(group) { Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    }
}
