// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace FolderSwitch.DemoGif;

/// <summary>One frame of the animation and its duration, in hundredths of a second.</summary>
internal sealed record Frame(BitmapSource Image, int Delay, bool Transient = false);

/// <summary>
/// Encodes the frames as an animated GIF that loops forever, with ImageMagick (Magick.NET): each frame gets its own
/// palette, optimized for its colours and without dithering (no noise on the flat areas of the windows, nothing that
/// changes from one frame to the next where the picture does not), then only what changes from the previous frame is
/// stored.
/// </summary>
internal static class GifEncoder
{
    private const uint TransientColors = 64;

    public static void Write(IReadOnlyList<Frame> frames, string path)
    {
        using var collection = new MagickImageCollection();
        foreach (var frame in frames)
        {
            var image = new MagickImage(PngBytes(frame.Image), MagickFormat.Png);
            image.Alpha(AlphaAction.Off);
            image.AnimationDelay = (uint)frame.Delay;
            image.AnimationTicksPerSecond = 100;
            image.GifDisposeMethod = GifDisposeMethod.None;
            collection.Add(image);
        }

        collection[0].AnimationIterations = 0;

        // On the frames as drawn, where an unchanged pixel is exactly the same: each frame is cropped to what changes,
        // and the pixels that do not change within it become transparent.
        collection.Optimize();
        collection.OptimizeTransparency();

        // Then the palettes: 255 colours, one entry left for the transparency. A crossfade frame is seen for 80 ms: fewer
        // colours are enough.
        for (var i = 0; i < collection.Count; i++)
        {
            var colours = frames[i].Transient ? TransientColors : 255u;
            collection[i].Quantize(new QuantizeSettings { Colors = colours, DitherMethod = DitherMethod.No, TreeDepth = 8 });
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        collection.Write(path, MagickFormat.Gif);
    }

    /// <summary>The number of frames of the GIF at <paramref name="path"/> and its total duration in seconds.</summary>
    public static (int Frames, double Seconds) Read(string path)
    {
        using var collection = new MagickImageCollection(path);
        return (collection.Count, collection.Sum(image => (double)image.AnimationDelay) / 100);
    }

    public static byte[] PngBytes(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
