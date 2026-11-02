// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Theming;

/// <summary>Colors of the item-count badge of a group header, both opaque <c>#rrggbb</c>.</summary>
/// <param name="Background">The pill, as it shows over the header (its tint already composited).</param>
/// <param name="Text">The count; always at least 4.5:1 against <paramref name="Background"/>.</param>
public sealed record CountBadgeVisual(string Background, string Text);
