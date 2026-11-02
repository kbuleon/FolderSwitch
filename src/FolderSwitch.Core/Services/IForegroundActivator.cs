// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>Brings a window to the foreground, which Windows does not always allow, and tells who has it.</summary>
public interface IForegroundActivator
{
    /// <summary>
    /// Whether the foreground window is one of this process's own (a window, a menu or a popup of it). Never throws:
    /// <c>false</c> when it cannot be told.
    /// </summary>
    bool IsOwnProcessInForeground();

    /// <summary>
    /// Restores <paramref name="hwnd"/> if it is minimized and makes it the foreground window, as far
    /// as Windows allows. Best effort: never throws.
    /// </summary>
    void Activate(nint hwnd);
}
