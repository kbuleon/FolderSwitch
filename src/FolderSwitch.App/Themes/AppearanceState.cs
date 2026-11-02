// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Theming;

namespace FolderSwitch.App.Themes;

/// <summary>
/// What the window is drawn with now, as one value: the resource <see cref="ThemeKeys.Appearance"/>. The views that
/// compute colors of their own (a group's, from its color) read it through a resource reference, so that they follow
/// every change of appearance.
/// </summary>
/// <param name="Theme">The theme drawn (a key of <see cref="ThemeCatalog.ThemeKeys"/>).</param>
/// <param name="Variant">Light or dark, as drawn (the Windows mode resolved for « Auto »).</param>
/// <param name="Tokens">The theme's colors and metrics, with the chosen background applied.</param>
/// <param name="RadiusPx">Corner radius of group blocks, in DIPs.</param>
internal sealed record AppearanceState(string Theme, Variant Variant, ThemeTokens Tokens, int RadiusPx);
