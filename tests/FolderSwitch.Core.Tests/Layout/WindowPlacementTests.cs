// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

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

    // --- CenterOnPoint ----------------------------------------------------------------------

    [Fact]
    public void CenterOnPoint_centers_exactly_on_a_point_in_the_middle()
    {
        var rect = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 960, 520, WorkArea);

        Assert.Equal(new RectD(560, 220, 800, 600), rect);
    }

    [Fact]
    public void CenterOnPoint_shifts_inside_near_the_left_edge_and_leaves_the_other_axis()
    {
        var rect = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 100, 520, WorkArea);

        Assert.Equal(new RectD(0, 220, 800, 600), rect);
    }

    [Fact]
    public void CenterOnPoint_shifts_inside_near_the_right_edge_and_leaves_the_other_axis()
    {
        var rect = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 1900, 520, WorkArea);

        Assert.Equal(new RectD(1120, 220, 800, 600), rect);
    }

    [Fact]
    public void CenterOnPoint_shifts_inside_near_the_top_edge_and_leaves_the_other_axis()
    {
        var rect = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 960, 10, WorkArea);

        Assert.Equal(new RectD(560, 0, 800, 600), rect);
    }

    [Fact]
    public void CenterOnPoint_shifts_inside_near_the_bottom_edge_and_leaves_the_other_axis()
    {
        var rect = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 960, 1030, WorkArea);

        Assert.Equal(new RectD(560, 440, 800, 600), rect);
    }

    [Fact]
    public void CenterOnPoint_shifts_inside_in_each_corner()
    {
        var size = new SizeD(800, 600);

        Assert.Equal(new RectD(0, 0, 800, 600), WindowPlacement.CenterOnPoint(size, 0, 0, WorkArea));
        Assert.Equal(new RectD(1120, 0, 800, 600), WindowPlacement.CenterOnPoint(size, 1920, 0, WorkArea));
        Assert.Equal(new RectD(0, 440, 800, 600), WindowPlacement.CenterOnPoint(size, 0, 1040, WorkArea));
        Assert.Equal(new RectD(1120, 440, 800, 600), WindowPlacement.CenterOnPoint(size, 1920, 1040, WorkArea));
    }

    [Fact]
    public void CenterOnPoint_brings_back_a_point_outside_the_work_area()
    {
        var rect = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 5000, 5000, WorkArea);

        Assert.Equal(new RectD(1120, 440, 800, 600), rect);
    }

    [Fact]
    public void CenterOnPoint_clamps_an_oversized_window_and_aligns_it_to_the_origin()
    {
        var rect = WindowPlacement.CenterOnPoint(new SizeD(3000, 2000), 500, 500, LeftMonitor);

        Assert.Equal(new RectD(-1920, 0, 1920, 1040), rect);
    }

    [Fact]
    public void CenterOnPoint_clamps_each_axis_independently()
    {
        // Too wide only: clamped and aligned horizontally, still centered on the point vertically.
        var wide = WindowPlacement.CenterOnPoint(new SizeD(3000, 600), 500, 520, WorkArea);
        Assert.Equal(new RectD(0, 220, 1920, 600), wide);

        // Too tall only: clamped and aligned vertically, shifted horizontally at the right edge.
        var tall = WindowPlacement.CenterOnPoint(new SizeD(800, 2000), 1800, 500, WorkArea);
        Assert.Equal(new RectD(1120, 0, 800, 1040), tall);
    }

    [Fact]
    public void CenterOnPoint_works_on_a_monitor_with_a_negative_origin()
    {
        var middle = WindowPlacement.CenterOnPoint(new SizeD(800, 600), -960, 520, LeftMonitor);
        Assert.Equal(new RectD(-1360, 220, 800, 600), middle);

        var leftEdge = WindowPlacement.CenterOnPoint(new SizeD(800, 600), -1900, 520, LeftMonitor);
        Assert.Equal(new RectD(-1920, 220, 800, 600), leftEdge);
        AssertInside(leftEdge, LeftMonitor);
    }

    [Fact]
    public void CenterOnPoint_honours_a_work_area_below_a_top_taskbar()
    {
        var middle = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 960, 560, TopTaskbar);
        Assert.Equal(new RectD(560, 260, 800, 600), middle);

        var top = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 960, 50, TopTaskbar);
        Assert.Equal(new RectD(560, 40, 800, 600), top);

        var bottom = WindowPlacement.CenterOnPoint(new SizeD(800, 600), 960, 1075, TopTaskbar);
        Assert.Equal(new RectD(560, 480, 800, 600), bottom);
    }

    [Theory]
    [InlineData(-5000, -5000, 800, 600)]
    [InlineData(5000, 5000, 800, 600)]
    [InlineData(0, 0, 3000, 2000)]
    [InlineData(960, 520, 1920, 1040)]
    [InlineData(-1920, 1040, 100, 100)]
    public void CenterOnPoint_never_overflows_the_work_area(double x, double y, double width, double height)
    {
        foreach (var workArea in new[] { WorkArea, LeftMonitor, TopTaskbar })
        {
            AssertInside(WindowPlacement.CenterOnPoint(new SizeD(width, height), x, y, workArea), workArea);
        }
    }

    // --- FitInside --------------------------------------------------------------------------

    [Fact]
    public void FitInside_leaves_a_rect_already_inside_unchanged()
    {
        var rect = new RectD(100, 50, 600, 400);

        Assert.Equal(rect, WindowPlacement.FitInside(rect, WorkArea));
    }

    [Fact]
    public void FitInside_leaves_a_rect_exactly_as_big_as_the_work_area_unchanged()
    {
        Assert.Equal(TopTaskbar, WindowPlacement.FitInside(TopTaskbar, TopTaskbar));
    }

    [Fact]
    public void FitInside_slides_back_a_rect_off_the_left_edge()
    {
        var rect = WindowPlacement.FitInside(new RectD(-100, 50, 600, 400), WorkArea);

        Assert.Equal(new RectD(0, 50, 600, 400), rect);
    }

    [Fact]
    public void FitInside_slides_back_a_rect_off_the_right_edge()
    {
        var rect = WindowPlacement.FitInside(new RectD(1500, 50, 600, 400), WorkArea);

        Assert.Equal(new RectD(1320, 50, 600, 400), rect);
    }

    [Fact]
    public void FitInside_slides_back_a_rect_off_the_top_edge()
    {
        var rect = WindowPlacement.FitInside(new RectD(100, -30, 600, 400), WorkArea);

        Assert.Equal(new RectD(100, 0, 600, 400), rect);
    }

    [Fact]
    public void FitInside_slides_back_a_rect_off_the_bottom_edge()
    {
        var rect = WindowPlacement.FitInside(new RectD(100, 800, 600, 400), WorkArea);

        Assert.Equal(new RectD(100, 640, 600, 400), rect);
    }

    [Fact]
    public void FitInside_slides_back_a_rect_off_a_corner()
    {
        var rect = WindowPlacement.FitInside(new RectD(1800, 900, 600, 400), WorkArea);

        Assert.Equal(new RectD(1320, 640, 600, 400), rect);
    }

    [Fact]
    public void FitInside_clamps_an_oversized_rect_and_aligns_it_to_the_origin()
    {
        var rect = WindowPlacement.FitInside(new RectD(-50, -50, 3000, 2000), WorkArea);

        Assert.Equal(new RectD(0, 0, 1920, 1040), rect);
    }

    [Fact]
    public void FitInside_clamps_each_axis_independently()
    {
        var wide = WindowPlacement.FitInside(new RectD(500, 100, 3000, 400), WorkArea);
        Assert.Equal(new RectD(0, 100, 1920, 400), wide);

        var tall = WindowPlacement.FitInside(new RectD(500, 100, 400, 2000), WorkArea);
        Assert.Equal(new RectD(500, 0, 400, 1040), tall);
    }

    [Fact]
    public void FitInside_works_on_a_monitor_with_a_negative_origin()
    {
        var rect = WindowPlacement.FitInside(new RectD(-2000, 20, 600, 400), LeftMonitor);

        Assert.Equal(new RectD(-1920, 20, 600, 400), rect);
        AssertInside(rect, LeftMonitor);
    }

    [Fact]
    public void FitInside_honours_a_work_area_below_a_top_taskbar()
    {
        var rect = WindowPlacement.FitInside(new RectD(100, 0, 600, 400), TopTaskbar);

        Assert.Equal(new RectD(100, 40, 600, 400), rect);
    }

    [Theory]
    [InlineData(-50, -50, 600, 400)]
    [InlineData(5000, 5000, 600, 400)]
    [InlineData(-5000, -5000, 600, 400)]
    [InlineData(0, 0, 2500, 300)]
    [InlineData(1900, 1000, 3000, 2000)]
    public void FitInside_never_overflows_the_work_area(double x, double y, double width, double height)
    {
        foreach (var workArea in new[] { WorkArea, LeftMonitor, TopTaskbar })
        {
            AssertInside(WindowPlacement.FitInside(new RectD(x, y, width, height), workArea), workArea);
        }
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
