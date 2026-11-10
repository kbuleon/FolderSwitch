// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FolderSwitch.DemoGif;

/// <summary>
/// One PNG of the application's diagnostic mode (<c>--snapshot</c>): windows and popups drawn on a flat grey
/// background, with their drop shadow. The background and the shadow are found and removed, so that the windows can be
/// drawn on another background with a shadow of the tour's own: no grey rectangle, no second frame.
/// </summary>
internal sealed class Snapshot
{
    private readonly byte[] _pixels;
    private readonly int _stride;

    // For each pixel, 0 for the background (and the shadow), else the number of the shape it belongs to.
    private readonly int[] _labels;

    private Snapshot(string path)
    {
        using (var stream = File.OpenRead(path))
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            Width = bitmap.PixelWidth;
            Height = bitmap.PixelHeight;
            _stride = Width * 4;
            _pixels = new byte[_stride * Height];
            bitmap.CopyPixels(_pixels, _stride, 0);
        }

        _labels = new int[Width * Height];
        var outside = FindBackground();
        Shapes = LabelShapes(outside);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>The windows and popups drawn, largest first: what is neither the background nor the shadow.</summary>
    public IReadOnlyList<Shape> Shapes { get; }

    /// <summary>The largest shape: the main window (with, for some snapshots, the popup drawn over it).</summary>
    public Shape Main => Shapes[0];

    public static Snapshot Load(string path) =>
        File.Exists(path) ? new Snapshot(path) : throw new FileNotFoundException($"The snapshot {path} is missing.", path);

    /// <summary>
    /// The pixels of <paramref name="shape"/> within its bounds, the background and the other shapes transparent.
    /// </summary>
    public BitmapSource Cutout(Shape shape)
    {
        var bounds = shape.Bounds;
        var stride = bounds.Width * 4;
        var pixels = new byte[stride * bounds.Height];
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                var source = ((bounds.Y + y) * Width) + bounds.X + x;
                if (_labels[source] != shape.Label)
                {
                    continue;
                }

                Array.Copy(_pixels, source * 4, pixels, (y * stride) + (x * 4), 4);
            }
        }

        var bitmap = BitmapSource.Create(bounds.Width, bounds.Height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>A rectangle of the snapshot as it is, opaque.</summary>
    public BitmapSource Crop(Int32Rect rect)
    {
        var bitmap = new CroppedBitmap(BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgra32, null, _pixels, _stride), rect);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>The bounds of the pixels of <paramref name="shape"/> that are pure white, or null when there is none.</summary>
    public Int32Rect? WhiteBounds(Shape shape)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        var bounds = shape.Bounds;
        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var i = (y * _stride) + (x * 4);
                if (_labels[(y * Width) + x] == shape.Label && _pixels[i] == 255 && _pixels[i + 1] == 255 && _pixels[i + 2] == 255)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < 0 ? null : new Int32Rect(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// The last row of <paramref name="shape"/> at least <paramref name="fraction"/> of its width wide: the bottom of a
    /// window when a narrower popup hangs below it.
    /// </summary>
    public int LastWideRow(Shape shape, double fraction)
    {
        var bounds = shape.Bounds;
        for (var y = bounds.Y + bounds.Height - 1; y >= bounds.Y; y--)
        {
            var count = 0;
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                if (_labels[(y * Width) + x] == shape.Label)
                {
                    count++;
                }
            }

            if (count >= fraction * bounds.Width)
            {
                return y;
            }
        }

        return bounds.Y + bounds.Height - 1;
    }

    /// <summary>The horizontal extent of <paramref name="shape"/> on the rows below <paramref name="row"/>.</summary>
    public (int Left, int Right) ExtentBelow(Shape shape, int row)
    {
        int left = int.MaxValue, right = -1;
        var bounds = shape.Bounds;
        for (var y = row + 1; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                if (_labels[(y * Width) + x] == shape.Label)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
            }
        }

        return right < 0 ? (bounds.X, bounds.X + bounds.Width - 1) : (left, right);
    }

    // The background is flat; the shadow darkens it: a pixel of either is the background's colour times a factor
    // (the ratio of its channels is kept), which no window's colour is. Filled from the edges, so that a grey inside a
    // window is never taken for the background.
    private bool[] FindBackground()
    {
        var background = (B: _pixels[0], G: _pixels[1], R: _pixels[2]);
        bool IsBackground(int index)
        {
            var i = index * 4;
            double b = _pixels[i], g = _pixels[i + 1], r = _pixels[i + 2];
            var k = b / background.B;
            return k is >= 0.5 and <= 1.03
                && Math.Abs(g - (k * background.G)) <= 3
                && Math.Abs(r - (k * background.R)) <= 3;
        }

        var outside = new bool[Width * Height];
        var queue = new Queue<int>();
        void Visit(int index)
        {
            if (!outside[index] && IsBackground(index))
            {
                outside[index] = true;
                queue.Enqueue(index);
            }
        }

        for (var x = 0; x < Width; x++)
        {
            Visit(x);
            Visit(((Height - 1) * Width) + x);
        }

        for (var y = 0; y < Height; y++)
        {
            Visit(y * Width);
            Visit((y * Width) + Width - 1);
        }

        while (queue.TryDequeue(out var index))
        {
            int x = index % Width, y = index / Width;
            if (x > 0) { Visit(index - 1); }
            if (x < Width - 1) { Visit(index + 1); }
            if (y > 0) { Visit(index - Width); }
            if (y < Height - 1) { Visit(index + Width); }
        }

        return outside;
    }

    private List<Shape> LabelShapes(bool[] outside)
    {
        var shapes = new List<Shape>();
        var queue = new Queue<int>();
        var next = 0;
        for (var start = 0; start < outside.Length; start++)
        {
            if (outside[start] || _labels[start] != 0)
            {
                continue;
            }

            var label = ++next;
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1, area = 0;
            _labels[start] = label;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var index))
            {
                int x = index % Width, y = index / Width;
                area++;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
                foreach (var neighbour in Neighbours(index, x, y))
                {
                    if (!outside[neighbour] && _labels[neighbour] == 0)
                    {
                        _labels[neighbour] = label;
                        queue.Enqueue(neighbour);
                    }
                }
            }

            // Stray pixels (an anti-aliased corner) are not a shape.
            if (area >= 2500)
            {
                shapes.Add(new Shape(label, new Int32Rect(left, top, right - left + 1, bottom - top + 1), area));
            }
            else
            {
                Relabel(label, left, top, right, bottom);
            }
        }

        if (shapes.Count == 0)
        {
            throw new InvalidDataException("The snapshot shows no window.");
        }

        return [.. shapes.OrderByDescending(shape => shape.Area)];
    }

    private IEnumerable<int> Neighbours(int index, int x, int y)
    {
        if (x > 0) { yield return index - 1; }
        if (x < Width - 1) { yield return index + 1; }
        if (y > 0) { yield return index - Width; }
        if (y < Height - 1) { yield return index + Width; }
    }

    private void Relabel(int label, int left, int top, int right, int bottom)
    {
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (_labels[(y * Width) + x] == label)
                {
                    _labels[(y * Width) + x] = -1;
                }
            }
        }
    }
}

/// <summary>One window or popup of a snapshot: its number, its bounds and its area in pixels.</summary>
internal sealed record Shape(int Label, Int32Rect Bounds, int Area);
