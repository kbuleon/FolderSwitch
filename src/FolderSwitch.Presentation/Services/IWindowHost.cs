// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Presentation.Services;

/// <summary>The resident main window, as seen from the view models.</summary>
public interface IWindowHost
{
    /// <summary>Hides the main window (it is never closed). Hiding a hidden window does nothing.</summary>
    void HideWindow();
}
