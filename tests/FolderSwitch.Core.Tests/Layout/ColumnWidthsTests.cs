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

    [Theory]
    [InlineData(0, 150)]
    [InlineData(149, 150)]
    [InlineData(150, 150)]
    [InlineData(151, 160)]
    [InlineData(160, 160)]
    [InlineData(333, 340)]
    [InlineData(333.4, 340)]
    [InlineData(519, 520)]
    [InlineData(520, 520)]
    [InlineData(521, 520)]
    [InlineData(1000, 520)]
    public void Auto_rounds_up_to_the_step_then_clamps(double measured, int expected)
    {
        Assert.Equal(expected, ColumnWidths.Auto(measured));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1000)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Auto_falls_back_to_the_minimum_for_negative_or_NaN(double measured)
    {
        Assert.Equal(150, ColumnWidths.Auto(measured));
    }

    [Fact]
    public void Auto_clamps_infinity_to_the_maximum()
    {
        Assert.Equal(520, ColumnWidths.Auto(double.PositiveInfinity));
    }

    [Fact]
    public void Auto_result_is_always_a_multiple_of_the_step_within_bounds()
    {
        for (var measured = 0.0; measured <= 700.0; measured += 0.5)
        {
            var width = ColumnWidths.Auto(measured);
            Assert.InRange(width, ColumnWidths.Min, ColumnWidths.Max);
            Assert.Equal(0, width % ColumnWidths.Step);
        }
    }

    [Fact]
    public void Effective_keeps_a_fixed_width_whatever_the_content()
    {
        Assert.Equal(330, ColumnWidths.Effective(330, 999));
        Assert.Equal(330, ColumnWidths.Effective(330, 0));
    }

    [Fact]
    public void Effective_without_a_fixed_width_follows_the_content()
    {
        Assert.Equal(200, ColumnWidths.Effective(null, 200));
        Assert.Equal(160, ColumnWidths.Effective(null, 151));
        Assert.Equal(520, ColumnWidths.Effective(null, 1000));
        Assert.Equal(150, ColumnWidths.Effective(null, 10));
    }

    [Theory]
    [InlineData(0, 150)]
    [InlineData(100, 150)]
    [InlineData(150, 150)]
    [InlineData(520, 520)]
    [InlineData(600, 520)]
    public void Effective_clamps_a_fixed_width_defensively(int fixedWidth, int expected)
    {
        Assert.Equal(expected, ColumnWidths.Effective(fixedWidth, 300));
    }
}
