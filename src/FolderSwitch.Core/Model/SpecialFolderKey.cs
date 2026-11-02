// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

/// <summary>Well-known Windows locations that have no fixed disk path (or whose path varies per user).</summary>
public enum SpecialFolderKey
{
    Desktop,
    Documents,
    Downloads,
    Pictures,
    Music,
    Videos,
    OneDrive,
    UserProfile,
    ThisPC,
    RecycleBin,
    ProgramFiles,
    RoamingAppData,
    LocalAppData,
    Temp,
    Startup,
    Windows,
}
