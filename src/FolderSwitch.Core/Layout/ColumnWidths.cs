// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Core.Layout;

/// <summary>
/// Column width rules, in device-independent pixels: 150 to 520 in steps of 10.
/// Every column has a stored width; <see cref="Default"/> is the initial value of the "default column width" setting.
/// </summary>
public static class ColumnWidths
{
    public const int Min = 150;
    public const int Max = 520;
    public const int Step = 10;
    public const int Default = 220;

    /// <summary>Rounds to the nearest multiple of <see cref="Step"/> (midpoints go up), then clamps to <see cref="Min"/>..<see cref="Max"/>.</summary>
    public static int Clamp(int requested)
    {
        // Clamping first is equivalent (both bounds are multiples of the step) and cannot overflow.
        var clamped = Math.Clamp(requested, Min, Max);
        return (clamped + Step / 2) / Step * Step;
    }
}
