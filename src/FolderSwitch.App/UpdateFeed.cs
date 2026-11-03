// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Diagnostics.CodeAnalysis;
using Velopack.Sources;

namespace FolderSwitch.App;

/// <summary>Where FolderSwitch looks for new versions.</summary>
/// <remarks>
/// The update check is one of the two kinds of network access of FolderSwitch (the other is the favicons of the
/// sites): the GitHub API is asked, without a token, for the
/// releases of the repository, then the <c>releases.win.json</c> feed of each release, and the package when the user
/// accepts an update. Nothing else is sent, and nothing is sent when <see cref="Url"/> is not a GitHub repository.
/// </remarks>
internal static class UpdateFeed
{
    private const string Host = "github.com";

    /// <summary>
    /// The address of the public repository whose releases are read, <c>https://github.com/&lt;owner&gt;/&lt;repository&gt;</c>.
    /// Any other address, an empty one included, disables the check for updates, and so does the « Check for updates »
    /// button of the settings.
    /// </summary>
    public const string Url = "https://github.com/kbuleon/FolderSwitch";

    /// <summary>
    /// Whether <paramref name="url"/> is the address of a GitHub repository: <c>https://github.com/&lt;owner&gt;/&lt;repository&gt;</c>,
    /// and nothing else (a final slash, the case of the host and surrounding spaces are tolerated). No other host, port,
    /// path, query, fragment or <c>.git</c> suffix.
    /// </summary>
    /// <param name="url">The address to read.</param>
    /// <param name="repository">The repository address without a final slash; meaningful only when this returns <c>true</c>.</param>
    /// <param name="name">The <c>&lt;owner&gt;/&lt;repository&gt;</c> pair, for the log; meaningful only when this returns <c>true</c>.</param>
    internal static bool TryParseRepository(string? url, [NotNullWhen(true)] out Uri? repository, out string name)
    {
        repository = null;
        name = string.Empty;

        var trimmed = url?.Trim();
        if (string.IsNullOrEmpty(trimmed)
            || trimmed.Contains('\\')
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.Host != Host
            || !uri.IsDefaultPort
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0)
        {
            return false;
        }

        var path = uri.AbsolutePath;
        if (path.EndsWith('/'))
        {
            path = path[..^1];
        }

        var segments = path.Split('/');
        if (segments.Length != 3
            || segments[0].Length != 0
            || !IsNamePart(segments[1])
            || !IsNamePart(segments[2])
            || segments[2].EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        name = $"{segments[1]}/{segments[2]}";
        repository = new Uri($"https://{Host}/{name}");
        return true;
    }

    /// <summary>
    /// The Velopack source for <paramref name="url"/>: the stable releases of the repository, read without a token;
    /// <c>null</c> when <paramref name="url"/> is not a GitHub repository (<see cref="TryParseRepository"/>).
    /// </summary>
    internal static GithubSource? CreateSource(string? url) =>
        TryParseRepository(url, out var repository, out _)
            ? new GithubSource(repository.ToString(), accessToken: null, prerelease: false)
            : null;

    private static bool IsNamePart(string part) =>
        part.Length != 0 && part.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
