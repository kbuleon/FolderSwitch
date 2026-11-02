// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>Moves an Open, Save As or folder-picker dialog to a folder.</summary>
public interface IFileDialogNavigator
{
    /// <returns><c>false</c> if the dialog could not be moved to <paramref name="folder"/>.</returns>
    bool Navigate(nint dialog, string folder);
}
