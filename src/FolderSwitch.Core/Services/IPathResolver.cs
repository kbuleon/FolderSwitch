// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Model;

namespace FolderSwitch.Core.Services;

/// <summary>Turns an item into something that can be opened, and checks that it is reachable.</summary>
public interface IPathResolver
{
    /// <summary>
    /// The path to open for <paramref name="item"/>: a disk or UNC path, or a <c>shell:</c> /
    /// <c>::{CLSID}</c> form for a virtual location. <c>null</c> if it cannot be resolved.
    /// </summary>
    string? Resolve(Item item);

    /// <summary>
    /// Whether the folder or file at <paramref name="path"/> exists and is reachable. Implementations
    /// should honour <paramref name="timeout"/>, but callers do not rely on it: they stop waiting on
    /// their own.
    /// </summary>
    Task<bool> ExistsAsync(string path, TimeSpan timeout);
}
