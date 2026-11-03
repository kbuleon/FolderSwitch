// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Model;

public sealed class Settings
{
    /// <summary>UI language code; <c>null</c> means follow the system language.</summary>
    public string? Language { get; set; }

    public Hotkey Hotkey { get; set; } = Hotkey.Default;

    public bool StartWithWindows { get; set; } = true;

    /// <summary>Whether FolderSwitch looks for a new version by itself, a minute after the start and then every day.</summary>
    public bool CheckForUpdatesAutomatically { get; set; } = true;

    public bool FirstRunCompleted { get; set; }

    public Guid? ActiveTabId { get; set; }
}
