// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>How an activation ended.</summary>
public enum ActivationOutcome
{
    /// <summary>The action was carried out in the calling window: the Explorer window or file dialog moved, or a tab opened in it.</summary>
    Done,

    /// <summary>The target could not be resolved, does not exist, or did not answer in time. Nothing was run.</summary>
    TargetUnavailable,

    /// <summary>The target looked fine but the action itself failed.</summary>
    Failed,

    /// <summary>
    /// The action failed and Windows has already told the user, with its own message (a file the shell could not open):
    /// nothing more is to be shown.
    /// </summary>
    ReportedByShell,

    /// <summary>
    /// The action was carried out outside the calling window: a new Explorer window, or a file in its application. That
    /// window or application is what comes to the front, not the calling window.
    /// </summary>
    OpenedElsewhere,
}
