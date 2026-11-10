// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Web;

/// <summary>The page a browser window shows, as read from the window.</summary>
/// <param name="Url">The address of the page: an absolute <c>http</c>, <c>https</c>, <c>ftp</c> or <c>file</c> address, normalized by <see cref="WebAddress.TryNormalize"/>.</param>
/// <param name="Title">The title of the page, <c>null</c> when none could be read.</param>
public sealed record BrowserPage(string Url, string? Title);
