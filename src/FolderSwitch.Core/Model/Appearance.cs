// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Layout;

namespace FolderSwitch.Core.Model;

public sealed class Appearance
{
    public ThemeMode Mode { get; set; } = ThemeMode.Light;

    public string Theme { get; set; } = "fluent";

    /// <summary>Background per theme and mode; keys look like <c>fluent:light</c> or <c>fluent:dark</c>.</summary>
    public Dictionary<string, string> Backgrounds { get; set; } = new();

    public GroupRadius Radius { get; set; } = GroupRadius.Theme;

    /// <summary>Width given to new columns and to "equalize widths", in pixels (150 to 520, steps of 10).</summary>
    public int DefaultColumnWidth { get; set; } = ColumnWidths.Default;
}
