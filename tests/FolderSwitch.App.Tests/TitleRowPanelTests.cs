// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using FolderSwitch.App.Views.Layout;

namespace FolderSwitch.App.Tests;

/// <summary>
/// The left of the title bar: the last child (Ko-fi's button) never widens the window, and is left out when it does
/// not fit whole. Laid out on an STA thread, without a window.
/// </summary>
public sealed class TitleRowPanelTests
{
    [Fact]
    public void The_row_never_asks_for_the_last_childs_width() => OnSta(() =>
    {
        var panel = Row(out _);

        panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Assert.Equal(new Size(120, 26), panel.DesiredSize);
    });

    [Fact]
    public void The_last_child_follows_the_others_when_it_fits() => OnSta(() =>
    {
        var panel = Row(out var last);

        Lay(panel, width: 220);

        Assert.True(panel.IsLastShown);
        Assert.Equal(new Point(120, 0), last.TranslatePoint(new Point(), panel));
        Assert.Equal(new Size(100, 26), last.RenderSize);
    });

    [Fact]
    public void The_last_child_is_left_out_when_it_does_not_fit_whole() => OnSta(() =>
    {
        var panel = Row(out var last);

        Lay(panel, width: 219);

        // Clipped to an empty slot: neither drawn nor hit.
        Assert.False(panel.IsLastShown);
        Assert.Equal(new Rect(), LayoutInformation.GetLayoutSlot(last));
        Assert.Equal(new Size(0, 0), LayoutInformation.GetLayoutClip(last)?.Bounds.Size);
    });

    [Fact]
    public void A_collapsed_last_child_is_not_shown() => OnSta(() =>
    {
        var panel = Row(out var last);
        last.Visibility = Visibility.Collapsed;

        Lay(panel, width: 400);

        Assert.False(panel.IsLastShown);
    });

    /// <summary>An icon and a name (120 wide together), then a 100 × 26 button.</summary>
    private static TitleRowPanel Row(out FrameworkElement last)
    {
        var panel = new TitleRowPanel();
        panel.Children.Add(new Border { Width = 16, Height = 16, Margin = new Thickness(0, 0, 9, 0) });
        panel.Children.Add(new Border { Width = 95, Height = 18 });
        last = new Border { Width = 100, Height = 26 };
        panel.Children.Add(last);
        return panel;
    }

    private static void Lay(TitleRowPanel panel, double width)
    {
        panel.Measure(new Size(width, 26));
        panel.Arrange(new Rect(0, 0, width, 26));
    }

    private static void OnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception e)
            {
                failure = ExceptionDispatchInfo.Capture(e);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
