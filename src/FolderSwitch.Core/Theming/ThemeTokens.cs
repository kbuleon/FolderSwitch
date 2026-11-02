// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Theming;

/// <summary>
/// Resolved colors and metrics of a theme in one variant. Colors are <c>#rrggbb</c>, except
/// <paramref name="Border"/> which may carry an alpha (<c>#aarrggbb</c>, WPF order).
/// </summary>
/// <param name="Background">Window background.</param>
/// <param name="Card">Surface of group blocks. For the Outline theme this is the background itself.</param>
/// <param name="Text">Text drawn on <paramref name="Card"/>.</param>
/// <param name="Muted">Secondary text drawn on <paramref name="Card"/>.</param>
/// <param name="Border">Hairline color.</param>
/// <param name="DefaultRadius">Group corner radius in px when the user keeps "theme default".</param>
/// <param name="GroupStyle">How groups are drawn.</param>
/// <param name="Dense">Tighter row spacing.</param>
/// <param name="UppercaseTitles">Group titles are shown in capitals.</param>
/// <param name="OnBackgroundText">Text drawn directly on <paramref name="Background"/> (tabs, title).</param>
/// <param name="OnBackgroundMuted">Secondary text drawn directly on <paramref name="Background"/>.</param>
public sealed record ThemeTokens(
    string Background,
    string Card,
    string Text,
    string Muted,
    string Border,
    int DefaultRadius,
    GroupStyle GroupStyle,
    bool Dense,
    bool UppercaseTitles,
    string OnBackgroundText,
    string OnBackgroundMuted);
