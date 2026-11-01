namespace FolderSwitch.Core.Web;

/// <summary>
/// Recognizes a favicon by the signature of its first bytes, never by what the server claims (its content type, the
/// extension of the address). Only the raster formats a WPF image can show are kept: an SVG, an HTML page or anything
/// else is refused.
/// </summary>
public static class FaviconFormat
{
    private static ReadOnlySpan<byte> Ico => [0x00, 0x00, 0x01, 0x00];

    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> Gif87 => "GIF87a"u8;

    private static ReadOnlySpan<byte> Gif89 => "GIF89a"u8;

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> Bmp => "BM"u8;

    /// <summary>A BMP file starts with a 14-byte header whose bytes 6 to 9 (reserved) are zero.</summary>
    private const int BmpHeaderLength = 14;

    /// <summary>
    /// The file extension, with its dot, for the format <paramref name="data"/> starts with: <c>.ico</c>, <c>.png</c>,
    /// <c>.gif</c>, <c>.jpg</c> or <c>.bmp</c>; <c>null</c> for any other content, including empty data.
    /// </summary>
    public static string? ExtensionOf(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(Png))
        {
            return ".png";
        }

        if (data.StartsWith(Ico))
        {
            return ".ico";
        }

        if (data.StartsWith(Gif87) || data.StartsWith(Gif89))
        {
            return ".gif";
        }

        if (data.StartsWith(Jpeg))
        {
            return ".jpg";
        }

        if (data.StartsWith(Bmp) && data.Length >= BmpHeaderLength && data.Slice(6, 4).IndexOfAnyExcept((byte)0) < 0)
        {
            return ".bmp";
        }

        return null;
    }
}
