// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Persistence;

/// <summary>What the first tab of a brand-new configuration contains.</summary>
public enum StartContent
{
    /// <summary>A tab holding a sample group of four special folders.</summary>
    SampleTab,

    /// <summary>A tab with two empty columns.</summary>
    EmptyTab,
}
