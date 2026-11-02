// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Karl BULEON
// Additional terms under GPLv3 section 7: see the License section of README.md.

namespace FolderSwitch.App.Diagnostics;

/// <summary>
/// Counts the exceptions the UI thread survived and trips when there are too many too close together (one thrown on
/// every layout pass, say): a resident application that keeps failing that way is better ended than left spinning.
/// </summary>
/// <remarks>UI thread only.</remarks>
internal sealed class ErrorBreaker
{
    /// <summary>Handled exceptions within <see cref="DefaultWindow"/> that trip the breaker.</summary>
    public const int DefaultLimit = 5;

    /// <summary>The span within which <see cref="DefaultLimit"/> exceptions trip the breaker.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(10);

    private readonly Queue<long> _recent = new();
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _time;

    public ErrorBreaker(TimeProvider time)
        : this(DefaultLimit, DefaultWindow, time)
    {
    }

    public ErrorBreaker(int limit, TimeSpan window, TimeProvider time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentNullException.ThrowIfNull(time);

        _limit = limit;
        _window = window;
        _time = time;
    }

    /// <summary>The handled exceptions counted within the window, as of the last <see cref="Record"/>.</summary>
    public int Count => _recent.Count;

    /// <summary>The span the breaker looks back over.</summary>
    public TimeSpan Window => _window;

    /// <summary>Counts one more handled exception; <c>true</c> when that makes the limit within the window.</summary>
    public bool Record()
    {
        var now = _time.GetTimestamp();
        _recent.Enqueue(now);
        while (_time.GetElapsedTime(_recent.Peek(), now) > _window)
        {
            _recent.Dequeue();
        }

        return _recent.Count >= _limit;
    }
}
