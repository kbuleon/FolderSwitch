using FolderSwitch.Core.Layout;

namespace FolderSwitch.Core.Tests.Layout;

public class WindowPlacementTests
{
    private static readonly RectD WorkArea = new(0, 0, 1920, 1040);

    // A secondary monitor placed left of the primary one: its origin is negative.
    private static readonly RectD LeftMonitor = new(-1920, 0, 1920, 1040);

    // A taskbar docked at the top shifts the work area down.
    private static readonly RectD TopTaskbar = new(0, 40, 1920, 1040);

    private static void AssertInside(RectD inner, RectD outer)
    {
        Assert.True(inner.X >= outer.X, $"left edge {inner.X} < {outer.X}");
        Assert.True(inner.Y >= outer.Y, $"top edge {inner.Y} < {outer.Y}");
        Assert.True(inner.X + inner.Width <= outer.X + outer.Width, $"right edge {inner.X + inner.Width} > {outer.X + outer.Width}");
        Assert.True(inner.Y + inner.Height <= outer.Y + outer.Height, $"bottom edge {inner.Y + inner.Height} > {outer.Y + outer.Height}");
    }

    // --- Geometry ---------------------------------------------------------------------------

    [Fact]
    public void Rect_edges_and_size()
    {
        var rect = new RectD(10, 20, 300, 400);

        Assert.Equal(310, rect.Right);
        Assert.Equal(420, rect.Bottom);
        Assert.Equal(new SizeD(300, 400), rect.Size);
    }

    // --- CenterOn ---------------------------------------------------------------------------

    [Fact]
    public void CenterOn_centers_in_the_work_area()
    {
        var rect = WindowPlacement.CenterOn(new SizeD(800, 600), WorkArea);

        Assert.Equal(new RectD(560, 220, 800, 600), rect);
    }

    [Fact]
    public void CenterOn_honours_a_work_area_that_does_not_start_at_the_origin()
    {
        var rect = WindowPlacement.CenterOn(new SizeD(800, 600), TopTaskbar);

        Assert.Equal(new RectD(560, 260, 800, 600), rect);
    }

    [Fact]
    public void CenterOn_works_on_a_monitor_with_a_negative_origin()
    {
        var rect = WindowPlacement.CenterOn(new SizeD(800, 600), LeftMonitor);

        Assert.Equal(new RectD(-1360, 220, 800, 600), rect);
        AssertInside(rect, LeftMonitor);
    }

    [Fact]
    public void CenterOn_clamps_an_oversized_window_and_aligns_it_to_the_origin()
    {
        var rect = WindowPlacement.CenterOn(new SizeD(3000, 2000), LeftMonitor);

        Assert.Equal(new RectD(-1920, 0, 1920, 1040), rect);
    }

    [Fact]
    public void CenterOn_clamps_each_axis_independently()
    {
        // Too wide only: clamped and aligned horizontally, still centered vertically.
        var wide = WindowPlacement.CenterOn(new SizeD(3000, 600), WorkArea);
        Assert.Equal(new RectD(0, 220, 1920, 600), wide);

        // Too tall only: clamped and aligned vertically, still centered horizontally.
        var tall = WindowPlacement.CenterOn(new SizeD(800, 2000), WorkArea);
        Assert.Equal(new RectD(560, 0, 800, 1040), tall);
    }

    [Fact]
    public void CenterOn_a_window_exactly_as_big_as_the_work_area_fills_it()
    {
        var rect = WindowPlacement.CenterOn(new SizeD(1920, 1040), TopTaskbar);

        Assert.Equal(TopTaskbar, rect);
    }

    // --- Reanchor ---------------------------------------------------------------------------

    [Fact]
    public void Reanchor_keeps_top_and_center()
    {
        var current = new RectD(100, 50, 600, 400);

        var rect = WindowPlacement.Reanchor(current, new SizeD(800, 300), WorkArea);

        // Center X was 400 and stays 400; the top edge is untouched.
        Assert.Equal(new RectD(0, 50, 800, 300), rect);
        Assert.Equal(400, rect.X + rect.Width / 2);
        Assert.Equal(50, rect.Y);
    }

    [Fact]
    public void Reanchor_keeps_top_and_center_when_the_window_shrinks()
    {
        var current = new RectD(500, 100, 800, 600);

        var rect = WindowPlacement.Reanchor(current, new SizeD(400, 200), WorkArea);

        Assert.Equal(new RectD(700, 100, 400, 200), rect);
    }

    [Fact]
    public void Reanchor_with_an_unchanged_size_does_not_move()
    {
        var current = new RectD(333, 77, 640, 480);

        var rect = WindowPlacement.Reanchor(current, current.Size, WorkArea);

        Assert.Equal(current, rect);
    }

    [Fact]
    public void Reanchor_clamps_into_the_work_area_on_the_left()
    {
        // Growing around the center would put the left edge at -100.
        var rect = WindowPlacement.Reanchor(new RectD(0, 10, 600, 400), new SizeD(800, 400), WorkArea);

        Assert.Equal(new RectD(0, 10, 800, 400), rect);
    }

    [Fact]
    public void Reanchor_clamps_into_the_work_area_on_the_right()
    {
        // Center 1700, new width 800 -> right edge at 2100.
        var rect = WindowPlacement.Reanchor(new RectD(1500, 10, 400, 300), new SizeD(800, 300), WorkArea);

        Assert.Equal(new RectD(1120, 10, 800, 300), rect);
    }

    [Fact]
    public void Reanchor_moves_up_only_when_the_bottom_overflows()
    {
        // Top edge 900 + height 400 would end at 1300.
        var rect = WindowPlacement.Reanchor(new RectD(100, 900, 600, 100), new SizeD(600, 400), WorkArea);

        Assert.Equal(new RectD(100, 640, 600, 400), rect);
    }

    [Fact]
    public void Reanchor_keeps_the_top_when_the_window_still_fits()
    {
        var rect = WindowPlacement.Reanchor(new RectD(100, 600, 600, 100), new SizeD(600, 400), WorkArea);

        Assert.Equal(600, rect.Y);
    }

    [Fact]
    public void Reanchor_clamps_an_oversized_content_to_the_work_area()
    {
        var rect = WindowPlacement.Reanchor(new RectD(500, 100, 600, 400), new SizeD(3000, 2000), WorkArea);

        Assert.Equal(new RectD(0, 0, 1920, 1040), rect);
    }

    [Fact]
    public void Reanchor_brings_a_window_that_starts_above_the_work_area_back_in()
    {
        var rect = WindowPlacement.Reanchor(new RectD(100, 0, 600, 400), new SizeD(600, 300), TopTaskbar);

        Assert.Equal(new RectD(100, 40, 600, 300), rect);
    }

    [Fact]
    public void Reanchor_works_on_a_monitor_with_a_negative_origin()
    {
        // Center X = -1600; width 800 -> left edge -2000, which is off the monitor.
        var rect = WindowPlacement.Reanchor(new RectD(-1900, 20, 600, 400), new SizeD(800, 400), LeftMonitor);

        Assert.Equal(new RectD(-1920, 20, 800, 400), rect);
        AssertInside(rect, LeftMonitor);
    }

    [Fact]
    public void Reanchor_keeps_the_center_on_a_monitor_with_a_negative_origin()
    {
        var rect = WindowPlacement.Reanchor(new RectD(-1500, 20, 600, 400), new SizeD(400, 400), LeftMonitor);

        Assert.Equal(new RectD(-1400, 20, 400, 400), rect);
    }

    [Theory]
    [InlineData(-50, -50, 600, 400, 800, 300)]
    [InlineData(0, 0, 600, 400, 2500, 300)]
    [InlineData(1800, 900, 100, 100, 1000, 900)]
    [InlineData(1900, 1000, 20, 20, 3000, 2000)]
    [InlineData(5000, 5000, 600, 400, 600, 400)]
    [InlineData(-5000, -5000, 600, 400, 600, 400)]
    [InlineData(960, 520, 0, 0, 1920, 1040)]
    public void Reanchor_never_overflows_the_work_area(
        double x, double y, double width, double height, double newWidth, double newHeight)
    {
        foreach (var workArea in new[] { WorkArea, LeftMonitor, TopTaskbar })
        {
            var rect = WindowPlacement.Reanchor(new RectD(x, y, width, height), new SizeD(newWidth, newHeight), workArea);

            AssertInside(rect, workArea);
        }
    }

    [Fact]
    public void Reanchor_does_not_throw_on_fractional_work_areas()
    {
        // (x + width) - width is not exactly x for these values.
        var workArea = new RectD(-1920.3, 0.1, 1919.7, 1039.9);

        var rect = WindowPlacement.Reanchor(new RectD(-1000.7, 10.3, 600.1, 400.7), new SizeD(5000, 5000), workArea);

        AssertInside(rect, workArea);
    }

    // --- ClampToWorkArea --------------------------------------------------------------------

    [Fact]
    public void ClampToWorkArea_caps_both_dimensions()
    {
        var size = WindowPlacement.ClampToWorkArea(new SizeD(3000, 2000), new RectD(0, 0, 1920, 1040));

        Assert.Equal(new SizeD(1920, 1040), size);
    }

    [Fact]
    public void ClampToWorkArea_clamps_width_and_height_independently()
    {
        Assert.Equal(new SizeD(1920, 500), WindowPlacement.ClampToWorkArea(new SizeD(3000, 500), WorkArea));
        Assert.Equal(new SizeD(800, 1040), WindowPlacement.ClampToWorkArea(new SizeD(800, 2000), WorkArea));
    }

    [Fact]
    public void ClampToWorkArea_leaves_a_size_that_fits_untouched()
    {
        Assert.Equal(new SizeD(800, 600), WindowPlacement.ClampToWorkArea(new SizeD(800, 600), WorkArea));
        Assert.Equal(new SizeD(1920, 1040), WindowPlacement.ClampToWorkArea(new SizeD(1920, 1040), WorkArea));
    }
}
