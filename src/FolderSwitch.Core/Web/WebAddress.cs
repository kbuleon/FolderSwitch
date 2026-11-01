using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FolderSwitch.Core.Web;

/// <summary>
/// Validation and normalization of the web addresses held by <see cref="Model.ItemKind.Url"/> items.
/// Only <c>http</c> and <c>https</c> are accepted; everything goes through <see cref="Uri"/>, so the
/// stored form is canonical (lower-case scheme and host, punycode host, percent-encoded path).
/// </summary>
public static class WebAddress
{
    private const string DefaultScheme = "https://";

    private static readonly IdnMapping Idn = new();

    /// <summary>
    /// Turns what the user typed (or what a browser reported) into the stored address. The input is trimmed,
    /// receives <c>https://</c> when it has no scheme (<c>localhost:8080</c> is a host and a port, not a
    /// scheme), and must be an absolute <c>http</c> or <c>https</c> address with a host.
    /// </summary>
    /// <param name="input">The raw address; <c>null</c> and blank are refused.</param>
    /// <param name="url">The normalized address (<see cref="Uri.AbsoluteUri"/> with a punycode host) when the result is <c>true</c>.</param>
    /// <returns><c>false</c> for another scheme (<c>javascript:</c>, <c>file:</c>, <c>mailto:</c>, <c>ftp:</c>), a missing host, credentials in the address (<c>user:password@host</c>) or an unparsable address.</returns>
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
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        // AbsoluteUri keeps an international host in Unicode: swap in the punycode form.
        url = new UriBuilder(uri) { Host = uri.IdnHost }.Uri.AbsoluteUri;
        return true;
    }

    /// <summary>
    /// True when <paramref name="url"/> is a valid web address that already carries its <c>http</c> or
    /// <c>https</c> scheme: what a stored item must hold (unlike user input, nothing is completed).
    /// </summary>
    public static bool IsWebUrl(string? url) =>
        url is not null
        && (url.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        && TryNormalize(url, out _);

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
    /// True when both addresses normalize to the same form, ignoring a trailing <c>/</c>
    /// (<c>https://Example.com</c> and <c>https://example.com/</c> are the same site). An address
    /// that does not normalize is never the same as another.
    /// </summary>
    public static bool SameSite(string a, string b) =>
        TryNormalize(a, out var first)
        && TryNormalize(b, out var second)
        && string.Equals(first.TrimEnd('/'), second.TrimEnd('/'), StringComparison.Ordinal);

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
