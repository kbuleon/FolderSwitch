// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderSwitch.DemoGif;

/// <summary>One still of the tour, how long it is held (in hundredths of a second) and its name.</summary>
internal sealed record Scene(string Name, BitmapSource Image, int Delay);

/// <summary>
/// The eight scenes of the tour, drawn from the 144 DPI snapshots of one language: the intro, six scenes that each
/// show the application (a text column on the left, the windows on the right) and the outro.
/// </summary>
internal sealed class Tour(string snapshots, string language, Captions captions)
{
    private const double TextLeft = 64;
    private const double TextWidth = 392;
    private const double TextTop = 112;
    private const double TextBottom = 628;

    // Where the windows go: right of the text column.
    private static readonly Rect ShotBox = new(500, 62, 744, 596);

    private static readonly Color Blue = Color.FromRgb(0x5B, 0xA4, 0xFF);
    private static readonly Color Amber = Color.FromRgb(0xFF, 0xB5, 0x47);
    private static readonly Color Green = Color.FromRgb(0x34, 0xD3, 0x99);
    private static readonly Color Violet = Color.FromRgb(0xC0, 0x84, 0xFC);
    private static readonly Color Cyan = Color.FromRgb(0x22, 0xD3, 0xEE);
    private static readonly Color Rose = Color.FromRgb(0xFB, 0x71, 0x85);

    private const int ProductScenes = 6;

    public IReadOnlyList<Scene> Build() =>
    [
        new("1-intro", Intro(), 250),
        new("2-board", Board(), 400),
        new("3-add", Add(), 400),
        new("4-edit", Edit(), 400),
        new("5-icons", Icons(), 400),
        new("6-context", Context(), 400),
        new("7-settings", Settings(), 400),
        new("8-outro", Outro(), 300),
    ];

    private BitmapSource Intro()
    {
        var root = Painter.Flat(context =>
        {
            Painter.Background(context, Cyan, new Point(Painter.Width / 2.0, 290), 640);
            const double center = Painter.Width / 2.0;
            const double top = 150;
            Painter.Icon(context, new Point(center - 66, top), 132);

            var title = Painter.Text("FolderSwitch", 80, Painter.TitleBrush, semibold: true);
            context.DrawText(title, new Point(center - (title.Width / 2), top + 148));

            var tagline = Centered(captions.Intro.Tagline, 31, Painter.BodyBrush, semibold: false, 1080);
            context.DrawText(tagline, new Point(center - 540, top + 272));

            var label = Painter.Text(captions.Intro.Label, 19, Painter.Frozen(new SolidColorBrush(Color.FromRgb(0xA5, 0xEE, 0xFA))), semibold: true);
            Painter.Pill(context, label, new Point(center, top + 272 + tagline.Height + 62), Cyan, 22, 10);
        });
        return Painter.Render(root);
    }

    private BitmapSource Outro()
    {
        var root = Painter.Flat(context =>
        {
            Painter.Background(context, Blue, new Point(Painter.Width / 2.0, 320), 640);
            const double center = Painter.Width / 2.0;

            var title = Painter.Text("FolderSwitch", 68, Painter.TitleBrush, semibold: true);
            const double iconSize = 92, gap = 24;
            var left = center - ((iconSize + gap + title.Width) / 2);
            Painter.Icon(context, new Point(left, 196), iconSize);
            context.DrawText(title, new Point(left + iconSize + gap, 196 + ((iconSize - title.Height) / 2)));

            var line = Centered(captions.Outro.Line, 31, Painter.BodyBrush, semibold: false, 1080);
            context.DrawText(line, new Point(center - 540, 340));

            var url = Painter.Text(captions.Outro.Url, 27, Painter.TitleBrush, semibold: true);
            Painter.Pill(context, url, new Point(center, 340 + line.Height + 80), Cyan, 30, 14);
        });
        return Painter.Render(root);
    }

    // Board: the window as the shortcut shows it, and the default shortcut's keys.
    private BitmapSource Board()
    {
        var snapshot = Load("");
        var window = snapshot.Main;
        var rect = Fit(window.Bounds.Width, window.Bounds.Height, 0.9);
        var image = snapshot.Cutout(window);
        return Product(0, Blue, captions.Board, rect, root => root.Children.Add(Painter.Lifted(context => context.DrawImage(image, rect))), Keys);
    }

    // Add: the window in Edit mode and its « Add ▾ » menu called from a browser, laid over the window's lower right corner.
    private BitmapSource Add()
    {
        var snapshot = Load("addmenu-browser");
        if (snapshot.Shapes.Count < 2)
        {
            throw new InvalidDataException("The snapshot of the Add menu does not show the menu apart from the window.");
        }

        var window = snapshot.Shapes[0].Bounds;
        var menu = snapshot.Shapes[1].Bounds;
        double menuX = window.Width - menu.Width + (0.12 * window.Width), menuY = window.Height - menu.Height - (0.05 * window.Height);
        double width = Math.Max(window.Width, menuX + menu.Width), height = Math.Max(window.Height, menuY + menu.Height);
        var all = Fit(width, height, 0.9);
        var scale = all.Width / width;
        var windowRect = new Rect(all.X, all.Y, window.Width * scale, window.Height * scale);
        var menuRect = new Rect(all.X + (menuX * scale), all.Y + (menuY * scale), menu.Width * scale, menu.Height * scale);
        var windowImage = snapshot.Cutout(snapshot.Shapes[0]);
        var menuImage = snapshot.Cutout(snapshot.Shapes[1]);
        return Product(1, Amber, captions.Add, all, root =>
        {
            root.Children.Add(Painter.Lifted(context => context.DrawImage(windowImage, windowRect)));
            root.Children.Add(Painter.Lifted(context => context.DrawImage(menuImage, menuRect)));
        });
    }

    // Edit: the wide window in Edit mode, large enough to be read: its title bar and first columns, the rest running
    // off the right and bottom edges.
    private BitmapSource Edit()
    {
        var snapshot = Load("customize");
        var window = snapshot.Main;
        const double scale = 0.74;
        var rect = new Rect(ShotBox.X, 92, window.Bounds.Width * scale, window.Bounds.Height * scale);
        var image = snapshot.Cutout(window);
        return Product(2, Green, captions.Edit, rect, root => root.Children.Add(Painter.Lifted(context => context.DrawImage(image, rect))));
    }

    // Icons: the icon picker of a group, whole, and the window above it running off the top and right edges.
    private BitmapSource Icons()
    {
        var snapshot = Load("iconpicker");
        var shape = snapshot.Main;
        var bounds = shape.Bounds;
        var windowBottom = snapshot.LastWideRow(shape, 0.9);
        var (_, pickerRight) = snapshot.ExtentBelow(shape, windowBottom);
        var scale = Math.Min(0.8, (Painter.Width - 22 - ShotBox.X) / (pickerRight + 1.0 - bounds.X));
        var rect = new Rect(ShotBox.X, Painter.Height - 30 - (bounds.Height * scale), bounds.Width * scale, bounds.Height * scale);
        var image = snapshot.Cutout(shape);
        return Product(3, Violet, captions.Icons, rect, root => root.Children.Add(Painter.Lifted(context => context.DrawImage(image, rect))));
    }

    // Context: the window called from a file dialog (its files and sites faded), over a sketch of that dialog.
    private BitmapSource Context()
    {
        var snapshot = Load("filedialog");
        var window = snapshot.Main.Bounds;
        double offsetX = 0.15 * window.Width, offsetY = 0.12 * window.Height;
        var all = Fit(window.Width + offsetX, window.Height + offsetY, 0.9);
        var scale = all.Width / (window.Width + offsetX);
        var dialogRect = new Rect(all.X, all.Y, 0.8 * window.Width * scale, 0.62 * window.Height * scale);
        var windowRect = new Rect(all.X + (offsetX * scale), all.Y + (offsetY * scale), window.Width * scale, window.Height * scale);
        var image = snapshot.Cutout(snapshot.Main);
        return Product(4, Cyan, captions.Context, all, root =>
        {
            root.Children.Add(Painter.Lifted(context => FileDialogSketch(context, dialogRect, captions.DialogTitle)));
            root.Children.Add(Painter.Lifted(context => context.DrawImage(image, windowRect)));
        });
    }

    // Settings: the Appearance page of the settings dialog, taken out of the dimmed window it covers.
    private BitmapSource Settings()
    {
        var snapshot = Load("settings-appearance");
        var dialog = snapshot.WhiteBounds(snapshot.Main) ?? snapshot.Main.Bounds;
        var rect = Fit(dialog.Width, dialog.Height, 0.9);
        var scale = rect.Width / dialog.Width;
        var image = snapshot.Crop(dialog);
        var clip = Painter.Frozen(new RectangleGeometry(rect, 12 * scale, 12 * scale));
        return Product(5, Rose, captions.Settings, rect, root => root.Children.Add(Painter.Lifted(context =>
        {
            context.PushClip(clip);
            context.DrawImage(image, rect);
            context.Pop();
        })));
    }

    /// <summary>
    /// A scene that shows the application: the background with its glow behind the windows, the name, the caption in
    /// the text column, the progress through the scenes, then the windows added by <paramref name="windows"/>.
    /// </summary>
    private BitmapSource Product(int index, Color accent, SceneCaption caption, Rect shots, Action<ContainerVisual> windows, Extra? extra = null)
    {
        var root = new ContainerVisual();
        var glow = new Point(
            Math.Clamp(shots.X + (shots.Width / 2), ShotBox.Left, ShotBox.Right),
            Math.Clamp(shots.Y + (shots.Height / 2), ShotBox.Top, ShotBox.Bottom));
        root.Children.Add(Painter.Flat(context =>
        {
            Painter.Background(context, accent, glow, 500);
            Brand(context);
            TextColumn(context, caption, accent, extra, index);
            Progress(context, index, accent);
        }));
        windows(root);
        return Painter.Render(root);
    }

    /// <summary>Something drawn under the text of a scene, at a scale of the text: its height, and how to draw it.</summary>
    private sealed record Extra(double Height, Action<DrawingContext, Point, double> Draw);

    private Extra Keys => new(46, (context, origin, scale) =>
    {
        var x = origin.X;
        for (var i = 0; i < captions.Keys.Count; i++)
        {
            if (i > 0)
            {
                var plus = Painter.Text("+", 24 * scale, Painter.BodyBrush, semibold: false);
                context.DrawText(plus, new Point(x + (12 * scale), origin.Y + (((44 * scale) - plus.Height) / 2)));
                x += plus.Width + (24 * scale);
            }

            x += Keycap(context, captions.Keys[i], new Point(x, origin.Y), scale);
        }
    });

    private static double Keycap(DrawingContext context, string key, Point origin, double scale)
    {
        var text = Painter.Text(key, 21 * scale, Painter.TitleBrush, semibold: true);
        var rect = new Rect(origin.X, origin.Y, text.Width + (34 * scale), 44 * scale);
        var radius = 9 * scale;
        context.DrawRoundedRectangle(Painter.Frozen(new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0x08))), null, new Rect(rect.X, rect.Y + (4 * scale), rect.Width, rect.Height), radius, radius);
        var fill = Painter.Frozen(new LinearGradientBrush(Color.FromArgb(0x48, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 90));
        var pen = new Pen(Painter.Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF))), 1.2);
        pen.Freeze();
        context.DrawRoundedRectangle(Painter.Frozen(new SolidColorBrush(Color.FromRgb(0x1C, 0x2A, 0x52))), null, rect, radius, radius);
        context.DrawRoundedRectangle(fill, pen, rect, radius, radius);
        context.DrawText(text, new Point(rect.X + (17 * scale), rect.Y + ((rect.Height - text.Height) / 2)));
        return rect.Width;
    }

    private static void Brand(DrawingContext context)
    {
        Painter.Icon(context, new Point(TextLeft, 40), 30);
        var name = Painter.Text("FolderSwitch", 21, Painter.Frozen(new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF))), semibold: true);
        context.DrawText(name, new Point(TextLeft + 40, 40 + ((30 - name.Height) / 2)));
    }

    private static void Progress(DrawingContext context, int index, Color accent)
    {
        var x = TextLeft;
        for (var i = 0; i < ProductScenes; i++)
        {
            var width = i == index ? 40.0 : 18.0;
            var brush = i == index ? Painter.Frozen(new SolidColorBrush(accent)) : Painter.DimBrush;
            context.DrawRoundedRectangle(brush, null, new Rect(x, 652, width, 6), 3, 3);
            x += width + 8;
        }
    }

    // The heading and the text, at the largest size (down to 60 %) that fits the column: no word wider than the
    // column, the whole block within its height. Centred vertically.
    private void TextColumn(DrawingContext context, SceneCaption caption, Color accent, Extra? extra, int index)
    {
        const double available = TextBottom - TextTop;
        for (var scale = 1.0; ; scale -= 0.04)
        {
            double titleSize = 41 * scale, bodySize = 21.5 * scale;
            var title = Painter.Text(caption.Title, titleSize, Painter.TitleBrush, semibold: true, TextWidth, 1.16);
            var body = Painter.Text(caption.Body, bodySize, Painter.BodyBrush, semibold: false, TextWidth, 1.45);
            double barGap = 24 * scale, titleGap = 20 * scale, extraGap = 30 * scale;
            var height = 5 + barGap + title.Height + titleGap + body.Height + (extra is null ? 0 : extraGap + (extra.Height * scale));
            var fits = height <= available
                && Painter.WordsFit(caption.Title, titleSize, semibold: true, TextWidth)
                && Painter.WordsFit(caption.Body, bodySize, semibold: false, TextWidth);
            if (!fits && scale > 0.6)
            {
                continue;
            }

            if (!fits || scale < 0.999)
            {
                Console.WriteLine($"  {language}, scene {index + 2}: text at {scale:P0}{(fits ? string.Empty : ", STILL TOO LONG")}");
            }

            var y = TextTop + ((available - height) / 2);
            context.DrawRoundedRectangle(Painter.Frozen(new SolidColorBrush(accent)), null, new Rect(TextLeft, y, 44, 5), 2.5, 2.5);
            y += 5 + barGap;
            context.DrawText(title, new Point(TextLeft, y));
            y += title.Height + titleGap;
            context.DrawText(body, new Point(TextLeft, y));
            y += body.Height;
            extra?.Draw(context, new Point(TextLeft, y + extraGap), scale);
            return;
        }
    }

    // A plain sketch of a Windows file dialog (title, address bar, navigation pane, list), seen behind the window.
    private static void FileDialogSketch(DrawingContext context, Rect rect, string title)
    {
        static Brush Solid(byte r, byte g, byte b) => Painter.Frozen(new SolidColorBrush(Color.FromRgb(r, g, b)));
        var border = new Pen(Solid(0xC8, 0xCB, 0xD2), 1);
        border.Freeze();
        context.DrawRoundedRectangle(Solid(0xF3, 0xF4, 0xF7), border, rect, 9, 9);

        var caption = Painter.Text(title, 15, Solid(0x1F, 0x23, 0x2B), semibold: false);
        context.DrawText(caption, new Point(rect.X + 16, rect.Y + 12));
        var cross = new Pen(Solid(0x4A, 0x4F, 0x5A), 1.4);
        cross.Freeze();
        var cx = rect.Right - 26;
        context.DrawLine(cross, new Point(cx - 5, rect.Y + 15), new Point(cx + 5, rect.Y + 25));
        context.DrawLine(cross, new Point(cx + 5, rect.Y + 15), new Point(cx - 5, rect.Y + 25));

        var white = Solid(0xFF, 0xFF, 0xFF);
        var line = new Pen(Solid(0xDD, 0xDF, 0xE4), 1);
        line.Freeze();
        context.DrawRoundedRectangle(white, line, new Rect(rect.X + 12, rect.Y + 44, rect.Width - 24, 28), 5, 5);
        context.DrawRoundedRectangle(white, line, new Rect(rect.X + 12, rect.Y + 82, rect.Width - 24, rect.Height - 94), 6, 6);

        var folder = Solid(0xF2, 0xB8, 0x3D);
        var bar = Solid(0xD5, 0xD8, 0xDE);
        for (var i = 0; i < 8; i++)
        {
            var y = rect.Y + 98 + (i * 30);
            if (y + 14 > rect.Bottom - 16)
            {
                break;
            }

            context.DrawRoundedRectangle(folder, null, new Rect(rect.X + 24, y, 16, 13), 2, 2);
            context.DrawRoundedRectangle(bar, null, new Rect(rect.X + 48, y + 3, 44 + (i * 13 % 36), 7), 3.5, 3.5);
        }
    }

    /// <summary>A rectangle of <paramref name="width"/> × <paramref name="height"/> scaled to fit the windows' box (at most
    /// <paramref name="maxScale"/>), centred in it.</summary>
    private static Rect Fit(double width, double height, double maxScale)
    {
        var scale = Math.Min(maxScale, Math.Min(ShotBox.Width / width, ShotBox.Height / height));
        double w = width * scale, h = height * scale;
        return new Rect(ShotBox.X + ((ShotBox.Width - w) / 2), ShotBox.Y + ((ShotBox.Height - h) / 2), w, h);
    }

    private static FormattedText Centered(string text, double size, Brush brush, bool semibold, double width)
    {
        var scale = 1.0;
        while (scale > 0.7 && !Painter.WordsFit(text, size * scale, semibold, width))
        {
            scale -= 0.05;
        }

        var formatted = Painter.Text(text, size * scale, brush, semibold, width, 1.3);
        formatted.TextAlignment = TextAlignment.Center;
        return formatted;
    }

    // <theme>-<variant>[-<kind>]-144dpi[-<language>].png; French, the reference language, has no suffix.
    private Snapshot Load(string kind)
    {
        var name = "fluent-light" + (kind.Length > 0 ? "-" + kind : string.Empty) + "-144dpi" + (language == "fr" ? string.Empty : "-" + language) + ".png";
        return Snapshot.Load(Path.Combine(snapshots, name));
    }
}
