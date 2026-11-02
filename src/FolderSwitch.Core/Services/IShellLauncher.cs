// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Services;

/// <summary>Opens files and web addresses through the Windows shell.</summary>
public interface IShellLauncher
{
    /// <summary>Opens the file with its associated application.</summary>
    /// <returns>
    /// <c>true</c> when the file was opened, or when the user declined a prompt of the shell; <c>false</c> when the shell
    /// could not open it and has already told the user so, with its own message.
    /// </returns>
    bool OpenFile(string path);

    /// <summary>Opens the web address in the default browser.</summary>
    /// <returns>
    /// <c>true</c> when the address was handed to the browser, or when the user declined a prompt of the shell;
    /// <c>false</c> when the address is not an <c>http</c> or <c>https</c> one (nothing is launched), or when the shell
    /// could not open it and has already told the user so, with its own message.
    /// </returns>
    bool OpenUrl(string url);
}
