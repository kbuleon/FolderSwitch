// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Buffers.Binary;
using System.IO;

namespace FolderSwitch.IconBuilder;

/// <summary>
/// The <c>.ico</c> file format, with PNG-compressed images (read by Windows Vista and later): a 6-byte header
/// (<c>ICONDIR</c>), one 16-byte directory entry per image (<c>ICONDIRENTRY</c>), then the PNG files one after the other.
/// </summary>
internal static class IcoFile
{
    private const int HeaderSize = 6;
    private const int EntrySize = 16;

    /// <summary>The widest and tallest image an entry can state (as a byte, where 0 stands for 256).</summary>
    private const int MaxSize = 256;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>An <c>.ico</c> holding these square images, in this order.</summary>
    public static byte[] Build(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // 1 = icon
        writer.Write((ushort)images.Count);

        var offset = HeaderSize + (EntrySize * images.Count);
        foreach (var (size, png) in images)
        {
            if (size is < 1 or > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(images), size, "An icon image is 1 to 256 pixels wide.");
            }

            var side = (byte)(size == MaxSize ? 0 : size);
            writer.Write(side); // width, 0 for 256
            writer.Write(side); // height, 0 for 256
            writer.Write((byte)0); // no palette
            writer.Write((byte)0); // reserved
            writer.Write((ushort)1); // planes
            writer.Write((ushort)32); // bits per pixel
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in images)
        {
            writer.Write(png);
        }

        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Reads the file back: every entry must lie inside the file, hold a PNG whose size is the one the entry states.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not a well-formed icon with PNG images.</exception>
    public static IReadOnlyList<IcoImage> Read(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < HeaderSize
            || BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0)) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2)) != 1)
        {
            throw new InvalidDataException("The file has no icon header.");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        if (data.Length < HeaderSize + (EntrySize * count))
        {
            throw new InvalidDataException("The icon directory is cut short.");
        }

        var images = new List<IcoImage>(count);
        for (var i = 0; i < count; i++)
        {
            var entry = data.AsSpan(HeaderSize + (EntrySize * i), EntrySize);
            var width = entry[0] == 0 ? MaxSize : entry[0];
            var height = entry[1] == 0 ? MaxSize : entry[1];
            var bits = BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]);
            var length = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);

            if (offset + (long)length > data.Length || length < 24)
            {
                throw new InvalidDataException($"Image {i + 1} lies outside the file.");
            }

            var png = data.AsSpan((int)offset, (int)length);
            if (!png[..PngSignature.Length].SequenceEqual(PngSignature))
            {
                throw new InvalidDataException($"Image {i + 1} is not a PNG file.");
            }

            // The IHDR chunk follows the signature: length, type, then width and height, big-endian.
            var pngWidth = BinaryPrimitives.ReadUInt32BigEndian(png[16..]);
            var pngHeight = BinaryPrimitives.ReadUInt32BigEndian(png[20..]);
            if (pngWidth != width || pngHeight != height)
            {
                throw new InvalidDataException(
                    $"Image {i + 1} states {width} × {height} but its PNG is {pngWidth} × {pngHeight}.");
            }

            images.Add(new IcoImage(width, height, bits, (int)length));
        }

        return images;
    }
}

/// <summary>One image of an <c>.ico</c> file, as its directory states it.</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="BitsPerPixel">Color depth.</param>
/// <param name="Length">Size of the PNG file in bytes.</param>
internal readonly record struct IcoImage(int Width, int Height, int BitsPerPixel, int Length);
