// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FolderSwitch.Core.Diagnostics;

namespace FolderSwitch.Core.Web;

/// <summary>
/// Retrieves the favicon of a web page: reads the page, tries the icons it declares in order, then
/// <c>/favicon.ico</c>, and keeps the first answer that is an image of a known format and is small enough.
/// </summary>
/// <remarks>
/// <para>
/// Every request is a plain <c>GET</c> that carries only the <c>User-Agent</c>: no cookie, no data of the user.
/// Limits: <see cref="DefaultTimeout"/> for the whole retrieval, <see cref="MaxBytes"/> read from any response, at most
/// <see cref="MaxRedirects"/> redirects, <c>http</c> and <c>https</c> addresses only.
/// </para>
/// <para>
/// Redirects are followed here, one by one, so that each target is checked (a redirect to <c>file:</c> is refused whatever
/// the handler does). The handler therefore must not follow them itself, and must not keep cookies: the production
/// <see cref="SocketsHttpHandler"/> is created with <c>AllowAutoRedirect = false</c> and <c>UseCookies = false</c>.
/// </para>
/// </remarks>
public sealed class FaviconFetcher
{
    /// <summary>The largest response read, for a page as for an image (256 KiB).</summary>
    public const int MaxBytes = 256 * 1024;

    /// <summary>The most redirects followed for one request.</summary>
    public const int MaxRedirects = 5;

    /// <summary>The time allowed for a whole retrieval: the page and every icon tried.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient client;
    private readonly string userAgent;
    private readonly TimeSpan timeout;

    /// <param name="handler">
    /// Sends the requests; not owned, never disposed here. It must neither follow redirects nor keep cookies (see the
    /// remarks of the class).
    /// </param>
    /// <param name="userAgent">The <c>User-Agent</c> header of every request, such as <c>FolderSwitch/1.5.0</c>.</param>
    /// <param name="timeout">The total time allowed for a retrieval; <see cref="DefaultTimeout"/> when <c>null</c>.</param>
    public FaviconFetcher(HttpMessageHandler handler, string userAgent, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        if (timeout is { } t && t <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "The timeout must be positive.");
        }

        // The total time is bounded by the token of each call, not by the client.
        client = new HttpClient(handler, disposeHandler: false) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        this.userAgent = userAgent;
        this.timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>
    /// Looks for the favicon of <paramref name="pageUrl"/>. An unreadable page still leads to <c>/favicon.ico</c> being
    /// tried. Any failure (network, status, size, format, time) gives <c>null</c>.
    /// </summary>
    /// <param name="pageUrl">An absolute <c>http</c> or <c>https</c> address; anything else gives <c>null</c> without a request.</param>
    /// <returns>The image and its extension (<c>.png</c>), or <c>null</c>.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled: the only exception ever thrown.</exception>
    public async Task<(byte[] Data, string Extension)?> FetchAsync(string pageUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var page) || !IsWeb(page))
        {
            return null;
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        try
        {
            return await FetchCoreAsync(page, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The total time is up.
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException && !CriticalExceptions.IsCritical(e))
        {
            ct.ThrowIfCancellationRequested();
            return null;
        }
    }

    private async Task<(byte[] Data, string Extension)?> FetchCoreAsync(Uri page, CancellationToken ct)
    {
        var html = string.Empty;
        var pageAddress = page;
        var response = await TryGetAsync(page, truncate: true, ct).ConfigureAwait(false);
        if (response is { } ok)
        {
            html = Encoding.UTF8.GetString(ok.Data);
            pageAddress = ok.Address;
        }

        foreach (var link in Candidates(html, pageAddress))
        {
            var icon = await TryGetAsync(link, truncate: false, ct).ConfigureAwait(false);
            if (icon is { } image && FaviconFormat.ExtensionOf(image.Data) is { } extension)
            {
                return (image.Data, extension);
            }
        }

        return null;
    }

    private static IReadOnlyList<Uri> Candidates(string html, Uri page)
    {
        try
        {
            return FaviconLinks.Find(html, page);
        }
        catch (RegexMatchTimeoutException)
        {
            // A page the analysis cannot read (pathological markup): the conventional address remains.
            return [new Uri(page, "/favicon.ico")];
        }
    }

    /// <summary>
    /// The body of a <c>200</c> answer, after the redirects. With <paramref name="truncate"/>, a longer body is cut at
    /// <see cref="MaxBytes"/>; without it, a longer body gives <c>null</c>. A failure of one request gives <c>null</c>
    /// as well, so that the next candidate is tried; only a cancellation propagates.
    /// </summary>
    private async Task<(Uri Address, byte[] Data)?> TryGetAsync(Uri start, bool truncate, CancellationToken ct)
    {
        try
        {
            var address = start;
            for (var redirects = 0; ; redirects++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, address);
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                if (IsRedirect(response.StatusCode))
                {
                    if (redirects == MaxRedirects
                        || response.Headers.Location is not { } location
                        || !Uri.TryCreate(address, location, out var next)
                        || !IsWeb(next))
                    {
                        return null;
                    }

                    address = next;
                    continue;
                }

                if (response.StatusCode != HttpStatusCode.OK
                    || (!truncate && response.Content.Headers.ContentLength > MaxBytes))
                {
                    return null;
                }

                var data = await ReadAsync(response.Content, truncate, ct).ConfigureAwait(false);
                return data is null ? null : (address, data);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Cancelled by the handler (its own timeout), not by the total time or the caller: try the next address.
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException && !CriticalExceptions.IsCritical(e))
        {
            ct.ThrowIfCancellationRequested();
            return null;
        }
    }

    private static async Task<byte[]?> ReadAsync(HttpContent content, bool truncate, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            var room = MaxBytes - (int)buffer.Length;
            if (read > room)
            {
                if (!truncate)
                {
                    return null;
                }

                buffer.Write(chunk, 0, room);
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static bool IsWeb(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
