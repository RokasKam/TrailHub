using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Trails.Domain.Trails;
using TrailHub.Trips.Domain.Trips;
using TrailHub.Weather.Domain.Forecasts;

namespace TrailHub.UnitTests;

public sealed class HikingConditionsTests
{
    [Fact]
    public void Thunderstorms_are_poor_hiking_weather() =>
        Assert.Equal(HikingSuitability.Poor, HikingConditions.Assess(Day(code: 95)).Suitability);

    [Fact]
    public void Strong_gusts_need_caution() =>
        Assert.Equal(HikingSuitability.Caution, HikingConditions.Assess(Day(gusts: 50)).Suitability);

    [Fact]
    public void Calm_clear_day_is_good()
    {
        var assessment = HikingConditions.Assess(Day());

        Assert.Equal(HikingSuitability.Good, assessment.Suitability);
        Assert.Empty(assessment.Warnings);
    }

    private static DailyForecast Day(int code = 0, double gusts = 10) =>
        new(new DateOnly(2026, 10, 2), code, 5, 18, 0, 0, 8, gusts, 3, DateTime.Today.AddHours(7), DateTime.Today.AddHours(18));
}
