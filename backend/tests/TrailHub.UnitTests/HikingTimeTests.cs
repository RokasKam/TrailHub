using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.UnitTests;

public sealed class HikingTimeTests
{
    [Fact]
    public void Flat_walking_is_four_km_per_hour()
    {
        Assert.Equal(180, HikingTime.EstimateMinutes(12_000, 0, 0));
    }

    [Fact]
    public void Adds_half_of_the_smaller_of_horizontal_and_vertical_time()
    {
        // 8 km = 2 h horizontal; 900 m up = 3 h vertical → 3 h + 1 h.
        Assert.Equal(240, HikingTime.EstimateMinutes(8_000, 900, 0));
    }

    [Fact]
    public void Rough_trails_take_longer()
    {
        Assert.True(HikingTime.EstimateMinutes(8_000, 900, 0, HikingTime.TerrainFactor(4)) > HikingTime.EstimateMinutes(8_000, 900, 0));
    }
}
