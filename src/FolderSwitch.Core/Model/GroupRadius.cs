// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>Corner radius of group blocks; <see cref="Theme"/> defers to the active theme.</summary>
public enum GroupRadius
{
    Theme,
    Square,
    Slight,
    Medium,
    Strong,
}
