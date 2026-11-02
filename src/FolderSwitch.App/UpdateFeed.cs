// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.App;

/// <summary>Where FolderSwitch looks for new versions.</summary>
/// <remarks>
/// The update check is the only network access of FolderSwitch: one <c>GET</c> of the feed file, and of the package
/// when the user accepts an update. Nothing else is sent, and nothing is sent when <see cref="Url"/> is empty.
/// </remarks>
internal static class UpdateFeed
{
    /// <summary>
    /// The address of the folder where the files made by <c>build/pack.ps1</c> are published (<c>releases.win.json</c>
    /// and the <c>.nupkg</c> packages), for instance <c>https://example.org/folderswitch/</c>. Empty by default: the
    /// check for updates is then disabled, and so is the « Check for updates » button of the settings. To be set when
    /// the site is known, before building the release. It must be HTTPS; plain HTTP is accepted only on this machine
    /// (<c>http://localhost:8000/</c>, for the manual test of an update), any other address disables the check.
    /// </summary>
    public const string Url = "";
}
