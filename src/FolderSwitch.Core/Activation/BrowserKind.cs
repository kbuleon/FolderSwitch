// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Activation;

/// <summary>A web browser FolderSwitch recognises as the window it was called from.</summary>
public enum BrowserKind
{
    /// <summary>Mozilla Firefox (<c>firefox.exe</c>, window class <c>MozillaWindowClass</c>).</summary>
    Firefox,

    /// <summary>Google Chrome (<c>chrome.exe</c>, window class <c>Chrome_WidgetWin_1</c>).</summary>
    Chrome,

    /// <summary>Microsoft Edge (<c>msedge.exe</c>, window class <c>Chrome_WidgetWin_1</c>).</summary>
    Edge,
}
