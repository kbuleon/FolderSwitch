// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Theming;

/// <summary>Colors of one group block, all <c>#rrggbb</c>.</summary>
/// <param name="HeaderBackground">Header fill.</param>
/// <param name="HeaderText">Title color; always at least 4.5:1 against <paramref name="HeaderBackground"/>.</param>
/// <param name="Accent">Edge, underline, border or top bar, depending on the group style.</param>
/// <param name="BodyBackground">Fill behind the items.</param>
/// <param name="ItemIcon">Folder icon color.</param>
public sealed record GroupVisual(
    string HeaderBackground,
    string HeaderText,
    string Accent,
    string BodyBackground,
    string ItemIcon);
