// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FolderSwitch.Core.Services;
using FolderSwitch.Windows.SystemServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace FolderSwitch.Windows.Shell;

/// <summary>
/// The small icon Windows associates with a file extension (<c>SHGetFileInfo</c>), as pixels.
/// </summary>
/// <remarks>
/// <para>
/// <b>No file is touched.</b> The icon is asked for a dummy name (<c>x</c> followed by the extension) with
/// <c>SHGFI_USEFILEATTRIBUTES</c>: the shell looks the extension up in the registry only. An extension Windows does not
/// know gets the generic file icon.
/// </para>
/// <para>
/// <b>Threads.</b> Any thread; the shell is called on a single-threaded apartment thread owned by this object, which the
/// caller waits for at most <see cref="Timeout"/>. One answer is kept per extension in lower case, a failure included
/// (nothing is asked twice, so an extension whose icon handler hangs costs one wait). Nothing is thrown.
/// </para>
/// </remarks>
public sealed class FileTypeIcons : IFileTypeIcons, IDisposable
{
    /// <summary>The longest the caller waits for the shell to give one icon.</summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private const int MaxExtensionLength = 64;

    private readonly ConcurrentDictionary<string, Lazy<IconPixels?>> _icons = new();
    private readonly StaWorker _worker = new("FolderSwitch file type icons");

    /// <inheritdoc/>
    public IconPixels? SmallIconFor(string extension)
    {
        if (!IsExtension(extension))
        {
            return null;
        }

        var key = extension.ToLowerInvariant();
        return _icons.GetOrAdd(key, k => new Lazy<IconPixels?>(() => Read(k))).Value;
    }

    /// <summary>Stops the shell thread; what is kept stays available.</summary>
    public void Dispose() => _worker.Dispose();

    private static bool IsExtension(string? extension) =>
        extension is { Length: > 1 and <= MaxExtensionLength }
        && extension[0] == '.'
        && extension.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private IconPixels? Read(string extension)
    {
        try
        {
            return _worker.TryRun(() => ReadOnShellThread("x" + extension), Timeout, out var pixels) ? pixels : null;
        }
        catch (Exception)
        {
            // A disposed worker, a failed interop call: no icon.
            return null;
        }
    }

    private static IconPixels? ReadOnShellThread(string dummyName)
    {
        try
        {
            var info = default(ShFileInfo);
            var result = ShellNative.SHGetFileInfo(
                dummyName,
                ShellNative.FileAttributeNormal,
                ref info,
                (uint)Marshal.SizeOf<ShFileInfo>(),
                ShellNative.ShgfiUseFileAttributes | ShellNative.ShgfiIcon | ShellNative.ShgfiSmallIcon);
            if (result == 0 || info.hIcon == 0)
            {
                return null;
            }

            var icon = new HICON(info.hIcon);
            try
            {
                return ToPixels(icon);
            }
            finally
            {
                PInvoke.DestroyIcon(icon);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The color bitmap of the icon as top-down 32-bit pixels. An icon without an alpha channel (older, 24-bit ones)
    /// takes its transparency from its mask.
    /// </summary>
    private static unsafe IconPixels? ToPixels(HICON icon)
    {
        var iconInfo = default(ICONINFO);
        if (!PInvoke.GetIconInfo(icon, &iconInfo))
        {
            return null;
        }

        var screen = PInvoke.GetDC(HWND.Null);
        try
        {
            if (iconInfo.hbmColor.IsNull || screen.IsNull)
            {
                // A monochrome icon (no color bitmap): not worth drawing.
                return null;
            }

            var bitmap = default(BITMAP);
            if (PInvoke.GetObject(iconInfo.hbmColor, sizeof(BITMAP), &bitmap) == 0 || bitmap.bmWidth <= 0 || bitmap.bmHeight <= 0)
            {
                return null;
            }

            var width = bitmap.bmWidth;
            var height = bitmap.bmHeight;
            var pixels = ReadBits(screen, iconInfo.hbmColor, width, height);
            if (pixels is null)
            {
                return null;
            }

            if (!HasAlpha(pixels) && !iconInfo.hbmMask.IsNull && ReadBits(screen, iconInfo.hbmMask, width, height) is { } mask)
            {
                // The mask is black where the icon is drawn and white where it is transparent.
                for (var i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i + 3] = mask[i] == 0 ? (byte)255 : (byte)0;
                }
            }

            return new IconPixels(width, height, pixels);
        }
        finally
        {
            if (!screen.IsNull)
            {
                PInvoke.ReleaseDC(HWND.Null, screen);
            }

            if (!iconInfo.hbmColor.IsNull)
            {
                PInvoke.DeleteObject(iconInfo.hbmColor);
            }

            if (!iconInfo.hbmMask.IsNull)
            {
                PInvoke.DeleteObject(iconInfo.hbmMask);
            }
        }
    }

    private static unsafe byte[]? ReadBits(HDC screen, HBITMAP bitmap, int width, int height)
    {
        var info = default(BITMAPINFO);
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = width;
        info.bmiHeader.biHeight = -height; // top-down
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = 0; // BI_RGB

        var pixels = new byte[checked(width * height * 4)];
        fixed (byte* buffer = pixels)
        {
            var lines = PInvoke.GetDIBits(screen, bitmap, 0, (uint)height, buffer, &info, DIB_USAGE.DIB_RGB_COLORS);
            return lines == height ? pixels : null;
        }
    }

    private static bool HasAlpha(byte[] pixels)
    {
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                return true;
            }
        }

        return false;
    }
}
