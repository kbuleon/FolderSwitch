// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FolderSwitch.Core.Web;

/// <summary>
/// Validation and normalization of the addresses held by <see cref="Model.ItemKind.Url"/> items (« sites »).
/// <c>http</c>, <c>https</c>, <c>ftp</c> and <c>file</c> are accepted; everything goes through <see cref="Uri"/>,
/// so the stored form is canonical (lower-case scheme and host, punycode host, percent-encoded path).
/// </summary>
public static class WebAddress
{
    private const string DefaultScheme = "https://";

    private static readonly IdnMapping Idn = new();

    /// <summary>
    /// Turns what the user typed (or what a browser reported) into the stored address. The input is trimmed,
    /// receives <c>https://</c> when it has no scheme (<c>localhost:8080</c> is a host and a port, not a
    /// scheme), and must be an absolute <c>http</c>, <c>https</c> or <c>ftp</c> address with a host, or a
    /// <c>file</c> address (<c>file:///C:/page.html</c>, or <c>file://server/share/page.html</c> with a host;
    /// <c>file://localhost/C:/page.html</c> is stored as <c>file:///C:/page.html</c>).
    /// </summary>
    /// <param name="input">The raw address; <c>null</c> and blank are refused.</param>
    /// <param name="url">The normalized address (<see cref="Uri.AbsoluteUri"/> with a punycode host) when the result is <c>true</c>.</param>
    /// <returns><c>false</c> for another scheme (<c>javascript:</c>, <c>mailto:</c>), a Windows path, a missing host (except for <c>file</c>), credentials in the address (<c>user:password@host</c>) or an unparsable address.</returns>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? url)
    {
        url = null;
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!HasScheme(text, out var otherScheme))
        {
            if (otherScheme)
            {
                return false;
            }

            text = DefaultScheme + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || !IsAccepted(uri.Scheme)
            || (string.IsNullOrEmpty(uri.Host) && uri.Scheme != Uri.UriSchemeFile)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // file://localhost/C:/... names the local machine, as file:///C:/... does: one stored form for both.
        if (uri.Scheme == Uri.UriSchemeFile && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            uri = new UriBuilder(uri) { Host = string.Empty }.Uri;
        }

        // A file address without a host (file:///C:/...) is kept as the Uri gives it.
        // AbsoluteUri keeps an international host in Unicode: swap in the punycode form.
        url = string.IsNullOrEmpty(uri.Host) ? uri.AbsoluteUri : new UriBuilder(uri) { Host = uri.IdnHost }.Uri.AbsoluteUri;
        return true;
    }

    /// <summary>
    /// True when <paramref name="url"/> is a valid address that already carries its scheme (<c>http</c>,
    /// <c>https</c>, <c>ftp</c> or <c>file</c>): what a stored item must hold (unlike user input, nothing is completed).
    /// </summary>
    public static bool IsSiteUrl(string? url)
    {
        if (url is null)
        {
            return false;
        }

        var text = url.TrimStart();
        return (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            && TryNormalize(url, out _);
    }

    /// <summary>
    /// The host of a <c>http</c> or <c>https</c> address (punycode form), <c>null</c> for any other scheme or an
    /// unparsable address: the only addresses a favicon is looked up for.
    /// </summary>
    public static string? WebHost(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.IdnHost)
            ? uri.IdnHost
            : null;

    /// <summary>
    /// The host shown for an address: the Unicode form of the host (<c>xn--caf-dma.fr</c> gives <c>café.fr</c>)
    /// without a leading <c>www.</c>. An address that cannot be parsed is returned as is.
    /// </summary>
    public static string HostLabel(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.IdnHost))
        {
            return url;
        }

        var host = uri.IdnHost;
        try
        {
            host = Idn.GetUnicode(host);
        }
        catch (ArgumentException)
        {
            // A host that is not valid punycode stays in its ASCII form.
        }

        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }

    /// <summary>
    /// The default name of a site: its host for <c>http</c>, <c>https</c> and <c>ftp</c> (<see cref="HostLabel"/>); for a
    /// <c>file</c> address the last segment of its path, percent-decoded (<c>index.html</c>), or the server of a
    /// <c>file://server/</c> address with no path. An address that cannot be parsed is returned as is.
    /// </summary>
    public static string DefaultName(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!TryFile(url, out var uri))
        {
            return HostLabel(url);
        }

        var segment = uri.Segments.Select(s => Uri.UnescapeDataString(s).Trim('/')).LastOrDefault(s => s.Length > 0);
        return segment ?? (uri.Host.Length > 0 ? HostLabel(url) : uri.LocalPath);
    }

    /// <summary>
    /// The location shown under the name of a site: the host for <c>http</c>, <c>https</c> and <c>ftp</c>
    /// (<see cref="HostLabel"/>); for a <c>file</c> address, the path in Windows form (<c>C:\dir\index.html</c>,
    /// <c>\\server\share\index.html</c>). An address that cannot be parsed is returned as is.
    /// </summary>
    public static string LocationLabel(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        return TryFile(url, out var uri) ? uri.LocalPath : HostLabel(url);
    }

    /// <summary>
    /// True when both addresses normalize to the same form, ignoring a trailing <c>/</c>
    /// (<c>https://Example.com</c> and <c>https://example.com/</c> are the same site). Two <c>file</c> addresses are
    /// compared percent-decoded and ignoring case, as Windows compares paths (<c>file:///C:/a.html</c> and
    /// <c>file:///c:/A.html</c> are the same page); any other path keeps its case. An address that does not normalize
    /// is never the same as another.
    /// </summary>
    public static bool SameSite(string a, string b)
    {
        if (!TryNormalize(a, out var first) || !TryNormalize(b, out var second))
        {
            return false;
        }

        first = first.TrimEnd('/');
        second = second.TrimEnd('/');
        return IsFile(first) && IsFile(second)
            ? string.Equals(Uri.UnescapeDataString(first), Uri.UnescapeDataString(second), StringComparison.OrdinalIgnoreCase)
            : string.Equals(first, second, StringComparison.Ordinal);
    }

    /// <summary>Whether a normalized address (its scheme in lower case) is a <c>file</c> one.</summary>
    private static bool IsFile(string normalized) => normalized.StartsWith("file:", StringComparison.Ordinal);

    /// <summary>The schemes a site may have.</summary>
    private static bool IsAccepted(string scheme) =>
        scheme is "http" or "https" or "ftp" or "file";

    /// <summary>Whether <paramref name="url"/> parses as a <c>file</c> address.</summary>
    private static bool TryFile(string url, [NotNullWhen(true)] out Uri? uri) =>
        Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeFile;

    /// <summary>
    /// True when <paramref name="text"/> starts with <c>scheme://</c>. When it starts with another
    /// <c>name:</c> that is not a port (<c>javascript:</c>, <c>mailto:</c>), <paramref name="otherScheme"/>
    /// is set: completing it with <c>https://</c> would turn it into a host with credentials.
    /// </summary>
    private static bool HasScheme(string text, out bool otherScheme)
    {
        otherScheme = false;
        var colon = text.IndexOf(':');
        if (colon <= 0 || !char.IsAsciiLetter(text[0]))
        {
            return false;
        }

        for (var i = 1; i < colon; i++)
        {
            if (!char.IsAsciiLetterOrDigit(text[i]) && text[i] != '+' && text[i] != '-' && text[i] != '.')
            {
                // Not a scheme name (a path, a query, an accent...): a host to complete.
                return false;
            }
        }

        if (text.AsSpan(colon + 1).StartsWith("//"))
        {
            return true;
        }

        // "host:8080" or "host:8080/path": digits after the colon make it a port.
        var afterColon = colon + 1;
        var digits = 0;
        while (afterColon + digits < text.Length && char.IsAsciiDigit(text[afterColon + digits]))
        {
            digits++;
        }

        var endsPort = afterColon + digits == text.Length || "/?#".Contains(text[afterColon + digits]);
        otherScheme = digits == 0 || !endsPort;
        return false;
    }
}
