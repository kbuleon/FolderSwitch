// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Collections.Concurrent;
using FolderSwitch.Core.Services;

namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>
/// A scripted <see cref="IFavicons"/>: nothing is cached at first; a fetch answers <see cref="Result"/> (after
/// <see cref="Gate"/> when it is set), and what it found is cached from then on, as the real cache does. Safe to call from
/// the thread pool.
/// </summary>
internal sealed class FakeFavicons : IFavicons
{
    private readonly ConcurrentQueue<string> _fetches = new();
    private readonly ConcurrentDictionary<string, string> _stored = new();

    /// <summary>The file a fetch finds for an address; by default none.</summary>
    public Func<string, string?> Result { get; set; } = _ => null;

    /// <summary>Whether a fetch that finds a file keeps it in the cache, as the real one does; <c>true</c> by default.</summary>
    public bool Caches { get; set; } = true;

    /// <summary>While set, a fetch waits for it to complete before it answers (a fetch still under way).</summary>
    public Task? Gate { get; set; }

    /// <summary>The addresses fetched, in order.</summary>
    public IReadOnlyCollection<string> Fetches => _fetches.ToArray();

    /// <summary>Puts a file in the cache before anything is fetched.</summary>
    public void Seed(string url, string path) => _stored[HostOf(url)] = path;

    public string? CachedPath(string url) => _stored.TryGetValue(HostOf(url), out var path) ? path : null;

    public async Task<string?> FetchAsync(string url, CancellationToken ct)
    {
        _fetches.Enqueue(url);
        if (Gate is { } gate)
        {
            await gate.ConfigureAwait(false);
        }

        var path = Result(url);
        if (path is not null && Caches)
        {
            _stored[HostOf(url)] = path;
        }

        return path;
    }

    private static string HostOf(string url) => new Uri(url).IdnHost;
}
