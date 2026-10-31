namespace FolderSwitch.Core.Tests.Persistence;

/// <summary>Hand-written clock: fixed local zone (UTC+02:00) and a manually advanced instant.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");

    private DateTimeOffset utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow;
    }

    public override TimeZoneInfo LocalTimeZone => Zone;

    public override DateTimeOffset GetUtcNow() => utcNow;

    public void Advance(TimeSpan by) => utcNow += by;
}
