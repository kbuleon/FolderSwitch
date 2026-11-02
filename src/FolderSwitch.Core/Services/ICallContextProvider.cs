// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Activation;

namespace FolderSwitch.Core.Services;

/// <summary>Captures the window FolderSwitch is called from.</summary>
public interface ICallContextProvider
{
    /// <summary>
    /// The context of the current foreground window: its kind and, for an Explorer window or a file
    /// dialog, the folder it shows when that can be read quickly. Must be called before FolderSwitch
    /// takes the focus. Never throws: <see cref="CallContext.None"/> when nothing can be captured.
    /// </summary>
    CallContext CaptureForeground();
}
