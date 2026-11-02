// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>What kind of window FolderSwitch was called from.</summary>
public enum CallContextKind
{
    /// <summary>Any window that is neither an Explorer window nor a file dialog (or no window at all).</summary>
    Other,

    /// <summary>A File Explorer window.</summary>
    Explorer,

    /// <summary>An Open, Save As or folder-picker dialog.</summary>
    FileDialog,

    /// <summary>A window of a web browser (see <see cref="BrowserKind"/>).</summary>
    Browser,
}
