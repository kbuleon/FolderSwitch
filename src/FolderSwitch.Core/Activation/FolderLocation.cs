// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>The folder an Explorer window or a file dialog shows, as <see cref="CallContext"/> holds it.</summary>
/// <param name="Folder">A disk path, a UNC path, or a <c>shell:</c> / <c>::{CLSID}</c> form (see <see cref="CallContext.CurrentFolder"/>).</param>
/// <param name="Name">The name the shell shows for it, when read (see <see cref="CallContext.CurrentFolderName"/>).</param>
public sealed record FolderLocation(string Folder, string? Name);
