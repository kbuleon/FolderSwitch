// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Presentation.ViewModels;

/// <summary>What the icon of a tab or group currently is, as the icon picker marks it.</summary>
public enum IconChoice
{
    /// <summary>Automatic: the emoji the name suggests, or nothing.</summary>
    Automatic,

    /// <summary>Chosen to be empty.</summary>
    None,

    /// <summary>A chosen emoji.</summary>
    Emoji,
}
