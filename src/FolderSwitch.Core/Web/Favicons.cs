// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Diagnostics;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Core.Web;

/// <summary>
/// <see cref="IFavicons"/> over a <see cref="FaviconCache"/> and a <see cref="FaviconFetcher"/>. The icon is kept per
/// host: two addresses of the same site share it. Only <c>http</c> and <c>https</c> addresses have one: <c>file</c> and
/// <c>ftp</c> addresses give <c>null</c> without touching the cache or the network.
/// </summary>
/// <param name="cache">Where the icons are kept.</param>
/// <param name="fetcher">Retrieves an icon from the network.</param>
/// <param name="log">Receives one line per failure (the host, never the full address); <c>null</c> logs nothing.</param>
public sealed class Favicons(FaviconCache cache, FaviconFetcher fetcher, Action<string>? log = null) : IFavicons
{
    private readonly FaviconCache cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly FaviconFetcher fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));

    /// <inheritdoc />
    public string? CachedPath(string url)
    {
        if (!TryHostOf(url, out _, out var host))
        {
            return null;
        }

        try
        {
            return cache.PathFor(host);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            log?.Invoke($"Favicon: cache unreadable for {host} ({e.GetType().Name}).");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        if (!TryHostOf(url, out var address, out var host))
        {
            return null;
        }

        try
        {
            var icon = await fetcher.FetchAsync(address, ct).ConfigureAwait(false);
            if (icon is null)
            {
                log?.Invoke($"Favicon: none retrieved for {host}.");
                return null;
            }

            return cache.Store(host, icon.Value.Data, icon.Value.Extension);
        }
        catch (Exception e) when (e is not OperationCanceledException && !CriticalExceptions.IsCritical(e))
        {
            ct.ThrowIfCancellationRequested();
            log?.Invoke($"Favicon: {host} could not be kept ({e.GetType().Name}: {e.Message}).");
            return null;
        }
    }

    private static bool TryHostOf(string url, out string address, out string host)
    {
        address = host = string.Empty;
        if (!WebAddress.TryNormalize(url, out var normalized) || WebAddress.WebHost(normalized) is not { } webHost)
        {
            // Not a web page (file, ftp): no icon, and no request at all.
            return false;
        }

        address = normalized;
        host = webHost;
        return true;
    }
}
