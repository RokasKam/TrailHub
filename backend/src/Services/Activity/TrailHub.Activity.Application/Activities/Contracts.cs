using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Application.Activities;

public interface IActivityRepository
{
    Task<HikeActivity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(HikeActivity activity);

    void Remove(HikeActivity activity);

    Task<PagedResult<ActivitySummaryDto>> GetPageAsync(string userId, PageRequest page, CancellationToken cancellationToken);

    Task<ActivityTotals> GetTotalsAsync(string userId, DateTime? sinceUtc, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonthlyStatisticsDto>> GetMonthlyAsync(string userId, DateTime sinceUtc, CancellationToken cancellationToken);
}

public sealed record ActivityTotals(
    int Hikes,
    double DistanceMeters,
    double ElevationGainMeters,
    long MovingDurationSeconds,
    double LongestHikeMeters,
    double? HighestElevationMeters);

public sealed record ActivitySummaryDto(
    Guid Id,
    string Name,
    Guid? TrailId,
    ActivitySource Source,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    double DistanceMeters,
    double ElevationGainMeters,
    long MovingDurationSeconds,
    double AverageSpeedMetersPerSecond);

public sealed record TrackPointDto(double Latitude, double Longitude, double? Elevation, DateTime TimestampUtc);

public sealed record ActivityDetailsDto(
    Guid Id,
    string Name,
    string? Notes,
    Guid? TrailId,
    ActivitySource Source,
    DateTime StartedAtUtc,
    DateTime EndedAtUtc,
    double DistanceMeters,
    double ElevationGainMeters,
    double ElevationLossMeters,
    double? MaxElevationMeters,
    long MovingDurationSeconds,
    long ElapsedDurationSeconds,
    double AverageSpeedMetersPerSecond,
    GeoBounds Bounds,
    IReadOnlyList<IReadOnlyList<TrackPointDto>> Segments,
    IReadOnlyList<ElevationSample> ElevationProfile);

public sealed record MonthlyStatisticsDto(int Year, int Month, int Hikes, double DistanceMeters, double ElevationGainMeters);

public sealed record HikingStatisticsDto(
    ActivityTotals AllTime,
    ActivityTotals ThisYear,
    IReadOnlyList<MonthlyStatisticsDto> LastTwelveMonths);

internal static class ActivityMappings
{
    public static ActivitySummaryDto ToSummary(this HikeActivity a) =>
        new(a.Id, a.Name, a.TrailId, a.Source, a.StartedAtUtc, a.EndedAtUtc, a.DistanceMeters, a.ElevationGainMeters, a.MovingDurationSeconds, a.AverageSpeedMetersPerSecond);
}
