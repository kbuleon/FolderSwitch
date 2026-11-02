// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>The names of keyboard keys as the user's keyboard layout labels them.</summary>
public interface IKeyNames
{
    /// <summary>The name of the key of <paramref name="virtualKey"/>, or <c>null</c> when the layout gives none.</summary>
    string? NameOf(int virtualKey);
}
