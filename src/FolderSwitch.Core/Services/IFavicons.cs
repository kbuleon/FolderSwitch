// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>The favicons of the web sites held by the board: what is kept on the disk, and how to get what is missing.</summary>
public interface IFavicons
{
    /// <summary>
    /// The file kept for the site of <paramref name="url"/> (one icon per host), or <c>null</c> when there is none or
    /// <paramref name="url"/> is not a web address. Reads the disk only: never the network.
    /// </summary>
    string? CachedPath(string url);

    /// <summary>
    /// Retrieves the icon of <paramref name="url"/> from the network, keeps it, and gives its file. Any failure gives
    /// <c>null</c> (a line goes to the log), writes nothing, and never throws, except for a cancellation.
    /// </summary>
    Task<string?> FetchAsync(string url, CancellationToken ct);
}
