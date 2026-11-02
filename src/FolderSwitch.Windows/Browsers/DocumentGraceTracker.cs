// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.Windows.Browsers;

/// <summary>
/// For <see cref="BrowserAddressReader"/>: how long each window has gone without a page document that confirms its
/// address bar, within one series of attempts. Not thread-safe: the reader calls it under its gate.
/// </summary>
/// <remarks>
/// A series is the attempts the Add menu makes every 200 ms while it is open. It ends when a window is not read for
/// more than <see cref="SeriesGap"/> (measured from the end of an attempt to the start of the next, so that a slow
/// attempt does not break it): the next attempt on that window starts a new series, and its wait starts over. Each
/// window has its own series, so that reading another window in between neither restarts nor ends it.
/// </remarks>
internal sealed class DocumentGraceTracker
{
    /// <summary>A pause between two attempts on a window longer than this ends its series (the caller's rhythm is 200 ms).</summary>
    public static readonly TimeSpan SeriesGap = TimeSpan.FromMilliseconds(600);

    private readonly TimeSpan _grace;
    private readonly Dictionary<nint, (TimeSpan Since, TimeSpan LastEnd)> _unconfirmed = [];

    /// <param name="grace">How long a window may go unconfirmed before its address bar is trusted alone.</param>
    public DocumentGraceTracker(TimeSpan grace)
    {
        _grace = grace;
    }

    /// <summary>
    /// Records an attempt on <paramref name="window"/> that ran from <paramref name="start"/> to <paramref name="end"/>
    /// (on any monotonic clock) and whether a document <paramref name="confirmed"/> its address bar. Returns whether the
    /// bar may be trusted alone: unconfirmed since the start of the series for at least the grace.
    /// </summary>
    public bool Trust(nint window, bool confirmed, TimeSpan start, TimeSpan end)
    {
        // Series that ended (on every window: the map stays small).
        foreach (var (key, value) in _unconfirmed)
        {
            if (start - value.LastEnd > SeriesGap)
            {
                _unconfirmed.Remove(key);
            }
        }

        if (confirmed)
        {
            _unconfirmed.Remove(window);
            return false;
        }

        var since = _unconfirmed.TryGetValue(window, out var series) ? series.Since : start;
        _unconfirmed[window] = (since, end);
        return end - since >= _grace;
    }
}
