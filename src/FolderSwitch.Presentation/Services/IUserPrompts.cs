// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Presentation.Services;

/// <summary>
/// Modal questions and pickers shown to the user. Every call blocks until the user answers; messages are
/// given as localization keys, translated by the implementation.
/// </summary>
public interface IUserPrompts
{
    /// <summary>Asks a yes/no question; true when the user confirms.</summary>
    bool Confirm(string messageKey);

    /// <summary>Shows an error message and waits until it is dismissed.</summary>
    void ShowError(string messageKey);

    /// <summary>The Windows folder picker; <c>null</c> when cancelled.</summary>
    string? PickFolder();

    /// <summary>The Windows file picker (open); <c>null</c> when cancelled.</summary>
    string? PickFile();

    /// <summary>Where to export the configuration (save dialog); <c>null</c> when cancelled.</summary>
    string? PickExportPath();

    /// <summary>Which configuration file to import (open dialog); <c>null</c> when cancelled.</summary>
    string? PickImportPath();

    /// <summary>
    /// Asks for a web address, the field starting with <paramref name="prefill"/>, or with the clipboard when it holds
    /// one and nothing is given. The normalized address (<c>http</c>, <c>https</c>, <c>ftp</c> or <c>file</c>); <c>null</c> when cancelled.
    /// </summary>
    string? AskUrl(string? prefill = null);
}
