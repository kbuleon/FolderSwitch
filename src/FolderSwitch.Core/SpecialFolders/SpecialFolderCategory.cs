// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.SpecialFolders;

/// <summary>How a special folder is grouped when the user picks one.</summary>
public enum SpecialFolderCategory
{
    /// <summary>Locations that belong to the user (Desktop, Documents, OneDrive, ...).</summary>
    Personal,

    /// <summary>Locations that belong to the machine or to Windows itself (This PC, Recycle Bin, ...).</summary>
    System,
}
