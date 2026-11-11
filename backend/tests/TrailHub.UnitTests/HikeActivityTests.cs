using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

public sealed class HikeActivityTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Pauses_count_towards_elapsed_but_not_moving_time_or_distance()
    {
        var first = Segment(0, 0, minutes: 60);         // 08:00–09:00
        var second = Segment(1, 0.05, minutes: 30, startOffsetMinutes: 120); // 10:00–10:30, starts ~5.5 km away

        var activity = HikeActivity.Create(Guid.NewGuid(), "u", "Hike", null, [first, second], ActivitySource.Recorded, null, Start.AddDays(1)).Value;

        Assert.Equal(90 * 60, activity.MovingDurationSeconds);
        Assert.Equal(150 * 60, activity.ElapsedDurationSeconds);
        Assert.InRange(activity.DistanceMeters, 1_000, 1_300); // two ~555 m segments, not the 5.5 km jump
    }

    [Fact]
    public void Timestamps_must_not_go_backwards()
    {
        var segment = new ActivitySegment([new(45, 10, null, Start.AddMinutes(5)), new(45.001, 10, null, Start)]);

        var result = HikeActivity.Create(Guid.NewGuid(), "u", "Hike", null, [segment], ActivitySource.Recorded, null, Start.AddDays(1));

        Assert.Equal(ActivityErrors.TimestampsOutOfOrder, result.Error);
    }

    [Fact]
    public void Requires_client_generated_id()
    {
        var result = HikeActivity.Create(Guid.Empty, "u", "Hike", null, [Segment(0, 0, 10)], ActivitySource.Recorded, null, Start.AddDays(1));

        Assert.Equal(ActivityErrors.MissingId, result.Error);
    }

    private static ActivitySegment Segment(int index, double latOffset, int minutes, int startOffsetMinutes = 0) =>
        new(Enumerable.Range(0, minutes + 1)
            .Select(m => new TrackPoint(45 + latOffset + m * 0.005 / minutes, 10, 500, Start.AddMinutes(startOffsetMinutes + m)))
            .ToList());
}
