namespace TrailHub.UnitTests;

internal sealed class FakeClock(DateTime start) : TimeProvider
{
    public DateTime Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);

    public void Advance(TimeSpan by) => Now += by;
}
