// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>An icon as pixels: <paramref name="Bgra32"/> holds <c>Width * Height</c> pixels, top row first, 4 bytes each (blue, green, red, alpha; the colors are not premultiplied).</summary>
public sealed record IconPixels(int Width, int Height, byte[] Bgra32);

/// <summary>The icons Windows associates with file types.</summary>
public interface IFileTypeIcons
{
    /// <summary>
    /// The small icon Windows shows for files of <paramref name="extension"/> (<c>.xlsx</c>, with its dot), read without
    /// touching any file; the generic file icon for an extension Windows does not know. <c>null</c> when there is no icon
    /// to give (an unusable extension, a failure): never throws. One answer per extension, whatever its case.
    /// </summary>
    IconPixels? SmallIconFor(string extension);
}
