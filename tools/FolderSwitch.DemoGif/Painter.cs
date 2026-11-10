// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using FolderSwitch.App.Views;

namespace FolderSwitch.DemoGif;

/// <summary>
/// The drawing primitives of the tour: its background, its text, the app icon and the windows of the snapshots with
/// a shadow. Everything is drawn in code, on a <see cref="Width"/> × <see cref="Height"/> canvas at one pixel per unit.
/// </summary>
internal static partial class Painter
{
    public const int Width = 1280;
    public const int Height = 720;

    public static readonly FontFamily Font = new("Segoe UI");

    /// <summary>The navy of the background, darker at the bottom right.</summary>
    private static readonly Color NavyTop = Color.FromRgb(0x10, 0x1D, 0x42);
    private static readonly Color NavyBottom = Color.FromRgb(0x08, 0x0E, 0x24);

    public static readonly Brush TitleBrush = Frozen(new SolidColorBrush(Colors.White));
    public static readonly Brush BodyBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xC5, 0xCE, 0xE3)));
    public static readonly Brush DimBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));

    /// <summary>Renders <paramref name="visual"/> into an opaque bitmap of the canvas' size.</summary>
    public static BitmapSource Render(Visual visual)
    {
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>The navy gradient and two soft glows: one of the scene's accent, one blue at the bottom left.</summary>
    /// <remarks>
    /// The glow of the accent, <paramref name="glowRadius"/> wide, is the only part that changes from one scene to the
    /// next: the rest stays the same, so that a crossfade only stores what it changes.
    /// </remarks>
    public static void Background(DrawingContext context, Color accent, Point glowCenter, double glowRadius)
    {
        var rect = new Rect(0, 0, Width, Height);
        context.DrawRectangle(Frozen(new LinearGradientBrush(NavyTop, NavyBottom, new Point(0, 0), new Point(1, 1))), null, rect);
        context.DrawRectangle(Glow(Color.FromRgb(0x25, 0x63, 0xEB), 0.22, new Point(Width * 0.04, Height * 0.98), 560, 400), null, rect);
        context.DrawRectangle(Glow(accent, 0.30, glowCenter, glowRadius, glowRadius * 0.74), null, rect);
    }

    /// <summary>The app icon, <paramref name="size"/> units wide, its top left corner at <paramref name="origin"/>.</summary>
    public static void Icon(DrawingContext context, Point origin, double size)
    {
        var scale = size / AppIconDrawing.Grid;
        context.PushTransform(new MatrixTransform(scale, 0, 0, scale, origin.X, origin.Y));
        AppIconDrawing.Draw(context);
        context.Pop();
    }

    /// <summary>
    /// Text set in Segoe UI (or Segoe UI Semibold), wrapped at <paramref name="maxWidth"/>. A word of one letter (Polish
    /// « z », « i », French « à », Portuguese « e »…) is kept with the next one, never left at the end of a line.
    /// </summary>
    public static FormattedText Text(string text, double size, Brush brush, bool semibold, double maxWidth = 0, double lineSpacing = 1.25)
    {
        text = SingleLetter().Replace(text, "$1 ");
        var typeface = new Typeface(Font, FontStyles.Normal, semibold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush, 1.0)
        {
            LineHeight = size * lineSpacing,
        };
        if (maxWidth > 0)
        {
            formatted.MaxTextWidth = maxWidth;
        }

        return formatted;
    }

    /// <summary>True when no word of <paramref name="text"/> is wider than <paramref name="maxWidth"/> at this size.</summary>
    public static bool WordsFit(string text, double size, bool semibold, double maxWidth) =>
        text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .All(word => Text(word, size, TitleBrush, semibold).WidthIncludingTrailingWhitespace <= maxWidth);

    /// <summary>A rounded pill around <paramref name="text"/>, centred on <paramref name="center"/>.</summary>
    public static Rect Pill(DrawingContext context, FormattedText text, Point center, Color accent, double paddingX, double paddingY)
    {
        var rect = new Rect(
            center.X - (text.Width / 2) - paddingX,
            center.Y - (text.Height / 2) - paddingY,
            text.Width + (2 * paddingX),
            text.Height + (2 * paddingY));
        var fill = Frozen(new SolidColorBrush(Color.FromArgb(0x24, accent.R, accent.G, accent.B)));
        var pen = new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x80, accent.R, accent.G, accent.B))), 1.5);
        pen.Freeze();
        context.DrawRoundedRectangle(fill, pen, rect, rect.Height / 2, rect.Height / 2);
        context.DrawText(text, new Point(rect.X + paddingX, rect.Y + paddingY));
        return rect;
    }

    /// <summary>
    /// A visual that draws <paramref name="draw"/> with the soft shadow of a window lifted off the background.
    /// </summary>
    public static DrawingVisual Lifted(Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual
        {
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x00, 0x04, 0x12),
                BlurRadius = 46,
                ShadowDepth = 14,
                Direction = 270,
                Opacity = 0.6,
                RenderingBias = RenderingBias.Quality,
            },
        };
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using var context = visual.RenderOpen();
        draw(context);
        return visual;
    }

    /// <summary>A plain visual, its bitmaps scaled with the best quality.</summary>
    public static DrawingVisual Flat(Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using var context = visual.RenderOpen();
        draw(context);
        return visual;
    }

    /// <summary>The blend of two frames: <paramref name="to"/> drawn over <paramref name="from"/> at <paramref name="amount"/>.</summary>
    public static BitmapSource Blend(BitmapSource from, BitmapSource to, double amount)
    {
        var rect = new Rect(0, 0, Width, Height);
        return Render(Flat(context =>
        {
            context.DrawImage(from, rect);
            context.PushOpacity(amount);
            context.DrawImage(to, rect);
            context.Pop();
        }));
    }

    public static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    [GeneratedRegex(@"(?<=^|\s)(\p{L}) ")]
    private static partial Regex SingleLetter();

    private static Brush Glow(Color color, double opacity, Point center, double radiusX, double radiusY)
    {
        var brush = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = center,
            GradientOrigin = center,
            RadiusX = radiusX,
            RadiusY = radiusY,
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * opacity), color.R, color.G, color.B), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * opacity * 0.4), color.R, color.G, color.B), 0.5));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        return Frozen(brush);
    }
}
