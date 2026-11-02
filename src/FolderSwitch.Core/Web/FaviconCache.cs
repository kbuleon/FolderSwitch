// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Globalization;

namespace FolderSwitch.Core.Web;

/// <summary>
/// The favicons kept on the disk, one file per host in a directory (by default under <c>%APPDATA%\FolderSwitch</c>,
/// outside the configuration and its export). The file is named after the host in lower-case punycode, plus the
/// extension of the image format (<c>xn--caf-dma.fr.png</c>). The directory is created by the first
/// <see cref="Store"/>; a missing directory is an empty cache.
/// </summary>
public sealed class FaviconCache
{
    private const string TemporaryExtension = ".tmp";

    private static readonly IdnMapping Idn = new();

    private readonly string directory;

    /// <param name="directory">Where the files are kept; created by the first <see cref="Store"/>.</param>
    public FaviconCache(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        this.directory = directory;
    }

    /// <summary>The file kept for <paramref name="host"/>, whatever its extension, or <c>null</c> when there is none.</summary>
    /// <exception cref="ArgumentException"><paramref name="host"/> cannot be turned into a file name.</exception>
    public string? PathFor(string host)
    {
        var name = FileNameOf(host);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        // "name.*" also matches the files of longer hosts (example.com.fr.png) and temporary files: keep the exact ones.
        return Directory.EnumerateFiles(directory, name + ".*")
            .Order(StringComparer.Ordinal)
            .FirstOrDefault(file => string.Equals(Path.GetFileNameWithoutExtension(file), name, StringComparison.Ordinal)
                && !string.Equals(Path.GetExtension(file), TemporaryExtension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Keeps <paramref name="data"/> as the favicon of <paramref name="host"/>, replacing any earlier one (whatever its
    /// extension). The file is written under a temporary name, then moved into place: a reader never sees a partial file.
    /// </summary>
    /// <param name="extension">The extension with its dot (<c>.png</c>), as given by <see cref="FaviconFormat"/>.</param>
    /// <returns>The full path of the file.</returns>
    /// <exception cref="ArgumentException"><paramref name="host"/> cannot be turned into a file name, or <paramref name="extension"/> is malformed.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    public string Store(string host, byte[] data, string extension)
    {
        ArgumentNullException.ThrowIfNull(data);
        var name = FileNameOf(host);
        if (!IsExtension(extension))
        {
            throw new ArgumentException("The extension must be a dot followed by letters or digits.", nameof(extension));
        }

        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, name + extension.ToLowerInvariant());
        var temporary = Path.Combine(directory, $"{name}.{Guid.NewGuid():N}{TemporaryExtension}");
        try
        {
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }

        // The previous icon of this host, if it had another extension.
        foreach (var other in Directory.EnumerateFiles(directory, name + ".*"))
        {
            if (!string.Equals(other, target, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileNameWithoutExtension(other), name, StringComparison.Ordinal)
                && !string.Equals(Path.GetExtension(other), TemporaryExtension, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(other);
            }
        }

        return target;
    }

    /// <summary>
    /// The host in lower-case punycode. The characters of an IPv6 address (<c>[</c>, <c>]</c>, <c>:</c>) become <c>_</c>;
    /// any other character a host cannot hold (<c>/</c>, <c>\</c>, an accent that punycode could not encode) is refused.
    /// </summary>
    private static string FileNameOf(string host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var name = host.Trim().ToLowerInvariant();
        try
        {
            name = Idn.GetAscii(name).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            // Not a valid international name (an IPv6 address, a label that is too long): used as typed.
        }

        var chars = new char[name.Length];
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            chars[i] = char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' ? c
                : c is '[' or ']' or ':' ? '_'
                : throw new ArgumentException("The host cannot be turned into a file name.", nameof(host));
        }

        name = new string(chars);
        if (name.Trim('.').Length == 0)
        {
            throw new ArgumentException("The host cannot be turned into a file name.", nameof(host));
        }

        return name;
    }

    /// <summary>A dot followed by at least one letter or digit.</summary>
    private static bool IsExtension(string extension) =>
        extension is { Length: > 1 } && extension[0] == '.' && extension.Skip(1).All(char.IsAsciiLetterOrDigit);

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover file is harmless (a temporary one is never read as an icon).
        }
    }
}
