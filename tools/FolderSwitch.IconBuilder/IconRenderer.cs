// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolderSwitch.App.Views;

namespace FolderSwitch.IconBuilder;

/// <summary>Draws the icon at one size and encodes it as PNG. Must be called on an STA thread.</summary>
internal static class IconRenderer
{
    /// <summary>
    /// The icon drawn natively at <paramref name="size"/> × <paramref name="size"/> pixels (every size is drawn from the
    /// vector geometry, none is scaled from another), with straight (not premultiplied) alpha.
    /// </summary>
    public static BitmapSource Render(int size)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(visual, EdgeMode.Unspecified);

        using (var context = visual.RenderOpen())
        {
            var scale = size / AppIconDrawing.Grid;
            context.PushTransform(new ScaleTransform(scale, scale));
            AppIconDrawing.Draw(context);
            context.Pop();
        }

        // 96 DPI: one device pixel per unit of the target, so that the size is the pixel size.
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var straight = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        straight.Freeze();
        return straight;
    }

    /// <summary>The PNG file's bytes: the same pixels give the same bytes.</summary>
    public static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
