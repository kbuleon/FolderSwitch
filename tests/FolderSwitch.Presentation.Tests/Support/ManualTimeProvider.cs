namespace FolderSwitch.Presentation.Tests.Support;

/// <summary>
/// A <see cref="TimeProvider"/> whose time moves only when <see cref="Advance"/> is called, firing the timers that fall
/// due on the way (outside any lock, in due order). <see cref="PendingTimers"/> tells when a delay is waiting.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<FakeTimer> _timers = [];
    private long _now = TimeSpan.TicksPerDay;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    /// <summary>Number of timers that are armed and have not fired yet.</summary>
    public int PendingTimers
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => timer.Due is not null);
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the time forward, firing every timer that falls due up to the new time.</summary>
    public void Advance(TimeSpan by)
    {
        var target = GetTimestamp() + by.Ticks;
        while (true)
        {
            FakeTimer? next;
            lock (_gate)
            {
                next = _timers.Where(timer => timer.Due is { } due && due <= target).MinBy(timer => timer.Due);
                if (next is null)
                {
                    _now = target;
                    return;
                }

                _now = Math.Max(_now, next.Due!.Value);
                next.Fired();
            }

            next.Run();
        }
    }

    private sealed class FakeTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private long _period;

        public long? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime.Ticks;
                _period = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
            }

            return true;
        }

        /// <summary>Called under the lock: disarms the timer, or re-arms it when periodic.</summary>
        public void Fired() => Due = _period > 0 ? Due + _period : null;

        public void Run() => callback(state);

        public void Dispose()
        {
            lock (owner._gate)
            {
                Due = null;
                owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
