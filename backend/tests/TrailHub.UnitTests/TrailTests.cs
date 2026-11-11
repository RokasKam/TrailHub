using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

public sealed class TrailTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_computes_metrics_and_naismith_duration()
    {
        // ~10 km due north climbing 600 m: Naismith = 2h flat + 1h climbing.
        var route = Enumerable.Range(0, 101).Select(i => new GeoPoint(45 + i * 0.000899, 10, 1000 + i * 6)).ToList();

        var trail = Trail.Create("Test", null, "Alps", Difficulty.Moderate, route, "user-1", Now).Value;

        Assert.InRange(trail.DistanceMeters, 9_950, 10_050);
        Assert.Equal(600, trail.ElevationGainMeters);
        Assert.InRange(trail.EstimatedDurationMinutes, 179, 181);
        Assert.False(trail.IsLoop);
    }

    [Fact]
    public void Route_needs_two_points()
    {
        var result = Trail.Create("Test", null, "Alps", Difficulty.Easy, [new GeoPoint(45, 10)], "u", Now);

        Assert.Equal(TrailErrors.RouteTooShort, result.Error);
    }

    [Fact]
    public void Start_close_to_end_is_a_loop()
    {
        var route = new List<GeoPoint> { new(45, 10), new(45.01, 10), new(45.01, 10.01), new(45.0005, 10) };

        Assert.True(Trail.Create("Loop", null, "Alps", Difficulty.Easy, route, "u", Now).Value.IsLoop);
    }

    [Fact]
    public void Only_creator_can_edit_details()
    {
        var trail = Trail.Create("Test", null, "Alps", Difficulty.Easy, [new(45, 10), new(45.1, 10)], "owner", Now).Value;

        Assert.Equal(TrailErrors.NotOwner, trail.UpdateDetails("New", null, "Alps", Difficulty.Hard, "someone-else").Error);
    }

    [Fact]
    public void Uploaded_photos_must_be_stored_under_the_trails_own_prefix()
    {
        var trail = Trail.Create("Test", null, "Alps", Difficulty.Easy, [new(45, 10), new(45.1, 10)], "u", Now).Value;
        var other = Guid.NewGuid();

        Assert.True(trail.AddUploadedPhoto($"{Trail.PhotoKeyPrefix(trail.Id)}a.jpg", "Summit", "u", Now).IsSuccess);
        Assert.Equal(TrailErrors.InvalidPhotoUrl, trail.AddUploadedPhoto($"{Trail.PhotoKeyPrefix(other)}a.jpg", null, "u", Now).Error);
        Assert.Equal(TrailErrors.InvalidPhotoUrl, trail.AddUploadedPhoto($"{Trail.PhotoKeyPrefix(trail.Id)}../x.jpg", null, "u", Now).Error);
        Assert.Single(trail.Photos);
    }

    [Theory]
    [InlineData("Drinking Water", PointOfInterestType.WaterSource)]
    [InlineData("Refuge Bonatti", PointOfInterestType.Shelter)]
    [InlineData("Campground", PointOfInterestType.Campsite)]
    [InlineData("Summit", PointOfInterestType.Viewpoint)]
    [InlineData("Parking", PointOfInterestType.Other)]
    public void Waypoint_names_map_to_poi_types(string text, PointOfInterestType expected) =>
        Assert.Equal(expected, PointOfInterest.InferType(text));
}
