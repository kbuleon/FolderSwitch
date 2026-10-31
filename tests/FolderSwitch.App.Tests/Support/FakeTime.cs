namespace FolderSwitch.App.Tests.Support;

/// <summary>A clock whose timestamps only move when the test advances them.</summary>
internal sealed class FakeTime : TimeProvider
{
    private long _ticks = TimeSpan.TicksPerDay;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}
