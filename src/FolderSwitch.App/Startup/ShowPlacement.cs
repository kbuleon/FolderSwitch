// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.App.Startup;

/// <summary>Where the hidden main window appears when it is shown: it depends on what showed it.</summary>
internal enum ShowPlacement
{
    /// <summary>
    /// The shortcut: centered on the mouse cursor, shifted the least possible to be fully inside the work area of the
    /// cursor's monitor.
    /// </summary>
    OnCursor,

    /// <summary>
    /// The notification icon (a click, « Open », « Customize ») or a second launch: centered on the work area of the
    /// monitor that holds the mouse cursor.
    /// </summary>
    OnScreen,
}
