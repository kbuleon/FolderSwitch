// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.Windows.Browsers;

namespace FolderSwitch.Windows.IntegrationTests;

/// <summary>
/// The series of attempts behind <see cref="BrowserAddressReader"/>'s wait for a page document: pure logic, on a
/// clock the test drives. Attempts take 20 ms and come every 200 ms, as the Add menu makes them.
/// </summary>
public sealed class DocumentGraceTrackerTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Rhythm = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan Attempt = TimeSpan.FromMilliseconds(20);

    private readonly DocumentGraceTracker _sut = new(Grace);

    [Fact]
    public void Continuous_series_trusts_the_bar_once_the_grace_has_passed()
    {
        var trusted = Series(window: 1, from: TimeSpan.Zero, attempts: 7);

        // Ends at 0.02, 0.22, ..., 1.02 s: trusted from the attempt that ends 1 s after the first one started.
        Assert.Equal([false, false, false, false, false, true, true], trusted);
    }

    [Fact]
    public void Slow_attempts_do_not_break_the_series()
    {
        // 450 ms attempts every 200 ms: 650 ms between two starts, but only 200 ms of pause.
        var start = TimeSpan.Zero;
        var trusted = new List<bool>();
        for (var i = 0; i < 4; i++)
        {
            trusted.Add(_sut.Trust(1, confirmed: false, start, start + TimeSpan.FromMilliseconds(450)));
            start += TimeSpan.FromMilliseconds(650);
        }

        // Ends at 0.45, 1.10, 1.75, 2.40 s.
        Assert.Equal([false, true, true, true], trusted);
    }

    [Fact]
    public void A_new_series_minutes_later_waits_again()
    {
        // A series that ended on the bar alone (an internal page, a hidden window)...
        Assert.True(Series(window: 1, from: TimeSpan.Zero, attempts: 7)[^1]);

        // ...does not let the next one, when the Add menu is opened again, trust the bar at once.
        var trusted = Series(window: 1, from: TimeSpan.FromMinutes(3), attempts: 7);

        Assert.Equal([false, false, false, false, false, true, true], trusted);
    }

    [Fact]
    public void A_pause_just_over_the_gap_starts_a_new_series_and_one_at_the_gap_does_not()
    {
        Series(window: 1, from: TimeSpan.Zero, attempts: 3);

        // The last attempt ended at 0.42 s; the next starts exactly 0.6 s later: same series, 1.02 s after its start.
        var atGap = TimeSpan.FromMilliseconds(420) + DocumentGraceTracker.SeriesGap;
        Assert.True(_sut.Trust(1, confirmed: false, atGap, atGap + Attempt));

        // Then a pause of 0.6 s + 1 ms: a new series.
        var overGap = atGap + Attempt + DocumentGraceTracker.SeriesGap + TimeSpan.FromMilliseconds(1);
        var trusted = Series(window: 1, from: overGap, attempts: 7);

        Assert.Equal([false, false, false, false, false, true, true], trusted);
    }

    [Fact]
    public void Alternating_windows_do_not_reset_each_other()
    {
        var first = new List<bool>();
        var second = new List<bool>();
        var start = TimeSpan.Zero;
        for (var i = 0; i < 7; i++)
        {
            first.Add(_sut.Trust(1, confirmed: false, start, start + Attempt));
            second.Add(_sut.Trust(2, confirmed: false, start + Attempt, start + Attempt + Attempt));
            start += Rhythm;
        }

        Assert.Equal([false, false, false, false, false, true, true], first);
        Assert.Equal([false, false, false, false, false, true, true], second);
    }

    [Fact]
    public void A_confirmed_attempt_ends_the_wait_and_the_next_unconfirmed_one_starts_over()
    {
        Series(window: 1, from: TimeSpan.Zero, attempts: 4);
        var start = 4 * Rhythm;

        Assert.False(_sut.Trust(1, confirmed: true, start, start + Attempt));

        // Text typed after the page was confirmed: a new wait.
        var trusted = Series(window: 1, from: start + Rhythm, attempts: 7);
        Assert.Equal([false, false, false, false, false, true, true], trusted);
    }

    /// <summary>Unconfirmed attempts on <paramref name="window"/> every 200 ms from <paramref name="from"/>.</summary>
    private List<bool> Series(nint window, TimeSpan from, int attempts)
    {
        var trusted = new List<bool>();
        for (var i = 0; i < attempts; i++)
        {
            var start = from + (i * Rhythm);
            trusted.Add(_sut.Trust(window, confirmed: false, start, start + Attempt));
        }

        return trusted;
    }
}
