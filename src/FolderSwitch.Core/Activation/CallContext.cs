// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>The window FolderSwitch was called from, captured when the hotkey fires.</summary>
/// <param name="Kind">Kind of the foreground window.</param>
/// <param name="Window">Handle of that window (<see cref="nint.Zero"/> when there is none).</param>
/// <param name="CurrentFolder">
/// The folder the window is showing, if known. A disk path, a UNC path, or a <c>shell:</c> /
/// <c>::{CLSID}</c> form for a virtual location (This PC, Recycle Bin...).
/// </param>
/// <param name="CurrentFolderName">
/// The name the shell shows for that folder (« Home », « Documents » for a library...), when it was read: the default
/// name of an item made from a virtual location, whose parsing name means nothing to a reader.
/// </param>
/// <param name="Browser">Which browser the window is, when <paramref name="Kind"/> is <see cref="CallContextKind.Browser"/>.</param>
public sealed record CallContext(
    CallContextKind Kind,
    nint Window,
    string? CurrentFolder,
    string? CurrentFolderName = null,
    BrowserKind? Browser = null)
{
    /// <summary>No usable calling window: behaves like <see cref="CallContextKind.Other"/>.</summary>
    public static CallContext None { get; } = new(CallContextKind.Other, nint.Zero, null);
}
