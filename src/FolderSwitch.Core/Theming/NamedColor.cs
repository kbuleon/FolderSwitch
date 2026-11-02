// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Theming;

/// <summary>A color of a catalog list: a stable key stored in the configuration and its <c>#rrggbb</c> value.</summary>
public sealed record NamedColor(string Key, string Hex);
