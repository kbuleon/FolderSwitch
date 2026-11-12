// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Core.Layout;

namespace FolderSwitch.Core.Tests.Layout;

public class ColumnWidthsTests
{
    [Fact]
    public void Constants_match_the_specification()
    {
        Assert.Equal(150, ColumnWidths.Min);
        Assert.Equal(520, ColumnWidths.Max);
        Assert.Equal(10, ColumnWidths.Step);
    }

    [Fact]
    public void Default_is_220_and_within_bounds()
    {
        Assert.Equal(220, ColumnWidths.Default);
        Assert.InRange(ColumnWidths.Default, ColumnWidths.Min, ColumnWidths.Max);
        Assert.Equal(0, ColumnWidths.Default % ColumnWidths.Step);
    }

    [Theory]
    [InlineData(100, 150)]
    [InlineData(150, 150)]
    [InlineData(154, 150)]
    [InlineData(155, 160)]
    [InlineData(333, 330)]
    [InlineData(520, 520)]
    [InlineData(900, 520)]
    public void Clamp_rounds_to_tens_and_clamps(int requested, int expected)
    {
        Assert.Equal(expected, ColumnWidths.Clamp(requested));
    }

    [Theory]
    [InlineData(int.MinValue, 150)]
    [InlineData(-1, 150)]
    [InlineData(int.MaxValue, 520)]
    public void Clamp_survives_extreme_values(int requested, int expected)
    {
        Assert.Equal(expected, ColumnWidths.Clamp(requested));
    }
}
