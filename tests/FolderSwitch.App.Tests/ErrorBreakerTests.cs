// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

using FolderSwitch.App.Diagnostics;
using FolderSwitch.App.Tests.Support;

namespace FolderSwitch.App.Tests;

/// <summary>Too many handled UI exceptions too close together make FolderSwitch quit.</summary>
public class ErrorBreakerTests
{
    private readonly FakeTime _time = new();

    [Fact]
    public void Defaults_are_five_within_ten_seconds()
    {
        Assert.Equal(5, ErrorBreaker.DefaultLimit);
        Assert.Equal(TimeSpan.FromSeconds(10), ErrorBreaker.DefaultWindow);
    }

    [Fact]
    public void Four_exceptions_within_the_window_do_not_trip_it()
    {
        var breaker = new ErrorBreaker(_time);

        for (var i = 0; i < 4; i++)
        {
            Assert.False(breaker.Record());
            _time.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(4, breaker.Count);
    }

    [Fact]
    public void The_fifth_exception_within_the_window_trips_it()
    {
        var breaker = new ErrorBreaker(_time);
        for (var i = 0; i < 4; i++)
        {
            breaker.Record();
            _time.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.True(breaker.Record());
        Assert.Equal(5, breaker.Count);
    }

    [Fact]
    public void Exceptions_older_than_the_window_are_forgotten()
    {
        var breaker = new ErrorBreaker(_time);

        // One every 3 s: never more than four within 10 s.
        for (var i = 0; i < 20; i++)
        {
            Assert.False(breaker.Record());
            _time.Advance(TimeSpan.FromSeconds(3));
        }

        Assert.True(breaker.Count <= 4);
    }

    [Fact]
    public void A_burst_after_a_quiet_spell_trips_it()
    {
        var breaker = new ErrorBreaker(_time);
        breaker.Record();
        breaker.Record();
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(breaker.Record());
        Assert.Equal(1, breaker.Count);
        Assert.False(breaker.Record());
        Assert.False(breaker.Record());
        Assert.False(breaker.Record());
        Assert.True(breaker.Record());
    }

    [Fact]
    public void Exactly_the_window_apart_still_counts()
    {
        var breaker = new ErrorBreaker(limit: 2, window: TimeSpan.FromSeconds(10), _time);
        breaker.Record();
        _time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(breaker.Record());
    }

    [Fact]
    public void A_limit_below_one_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ErrorBreaker(0, TimeSpan.FromSeconds(1), _time));
    }
}
