using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Domain.Activities;

public sealed record TrackPoint(double Latitude, double Longitude, double? Elevation, DateTime TimestampUtc)
{
    public GeoPoint ToGeoPoint() => new(Latitude, Longitude, Elevation);
}

/// <summary>A continuous stretch of recording. Pausing on the device ends a segment; resuming starts a new one.</summary>
public sealed record ActivitySegment(IReadOnlyList<TrackPoint> Points);

public enum ActivitySource
{
    Recorded,
    ImportedGpx,
}

/// <summary>
/// A completed hike. Recording happens on the device (so it works offline); the finished activity
/// is uploaded once with a client-generated id, which makes retries after a lost connection safe.
/// </summary>
public sealed class HikeActivity : AggregateRoot<Guid>
{
    public const int MaxNameLength = 200;
    public const int MaxNotesLength = 4000;
    public const int MaxPoints = 100_000;

    /// <summary>Tolerates small device clock drift when rejecting timestamps from the future.</summary>
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(10);

    private HikeActivity()
    {
    }

    public string UserId { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Notes { get; private set; }

    public Guid? TrailId { get; private set; }

    /// <summary>The planned route (Routes service) followed, if the hike was started from one.</summary>
    public Guid? RouteId { get; private set; }

    public ActivitySource Source { get; private set; }

    public IReadOnlyList<ActivitySegment> Segments { get; private set; } = [];

    public DateTime StartedAtUtc { get; private set; }

    public DateTime EndedAtUtc { get; private set; }

    public double DistanceMeters { get; private set; }

    public double ElevationGainMeters { get; private set; }

    public double ElevationLossMeters { get; private set; }

    public double? MaxElevationMeters { get; private set; }

    /// <summary>Time actually recording, i.e. excluding pauses.</summary>
    public long MovingDurationSeconds { get; private set; }

    public long ElapsedDurationSeconds { get; private set; }

    public double AverageSpeedMetersPerSecond { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public IEnumerable<TrackPoint> AllPoints => Segments.SelectMany(s => s.Points);

    public static Result<HikeActivity> Create(
        Guid id,
        string userId,
        string name,
        Guid? trailId,
        IReadOnlyList<ActivitySegment> segments,
        ActivitySource source,
        string? notes,
        DateTime nowUtc)
    {
        if (id == Guid.Empty)
        {
            return ActivityErrors.MissingId;
        }

        if (ValidateText(name, notes) is { } textError)
        {
            return textError;
        }

        var nonEmpty = segments.Where(s => s.Points.Count > 0).ToList();
        var pointCount = nonEmpty.Sum(s => s.Points.Count);
        if (pointCount < 2)
        {
            return ActivityErrors.TooFewPoints;
        }

        if (pointCount > MaxPoints)
        {
            return ActivityErrors.TooManyPoints;
        }

        DateTime? previous = null;
        foreach (var point in nonEmpty.SelectMany(s => s.Points))
        {
            if (GeoPoint.Create(point.Latitude, point.Longitude).Error is { } invalid)
            {
                return invalid;
            }

            if (previous > point.TimestampUtc)
            {
                return ActivityErrors.TimestampsOutOfOrder;
            }

            previous = point.TimestampUtc;
        }

        var startedAt = nonEmpty[0].Points[0].TimestampUtc;
        var endedAt = nonEmpty[^1].Points[^1].TimestampUtc;
        if (endedAt > nowUtc + FutureTolerance)
        {
            return ActivityErrors.TimestampsInFuture;
        }

        var activity = new HikeActivity
        {
            Id = id,
            UserId = userId,
            Name = name.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            TrailId = trailId,
            Source = source,
            Segments = nonEmpty,
            StartedAtUtc = startedAt,
            EndedAtUtc = endedAt,
            ElapsedDurationSeconds = (long)(endedAt - startedAt).TotalSeconds,
            CreatedAtUtc = nowUtc,
        };
        activity.CalculateMetrics();
        return activity;
    }

    public void FollowedRoute(Guid? routeId) => RouteId = routeId;

    public Result UpdateDetails(string name, string? notes)
    {
        if (ValidateText(name, notes) is { } error)
        {
            return error;
        }

        Name = name.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return Result.Success();
    }

    private void CalculateMetrics()
    {
        // Metrics are per segment: the gap between a pause and a resume isn't walked distance.
        double distance = 0, gain = 0, loss = 0;
        double? max = null;
        long moving = 0;
        foreach (var segment in Segments)
        {
            var metrics = RouteMetrics.Calculate(segment.Points.Select(p => p.ToGeoPoint()).ToList());
            distance += metrics.DistanceMeters;
            gain += metrics.ElevationGainMeters;
            loss += metrics.ElevationLossMeters;
            if (metrics.MaxElevationMeters is { } segmentMax)
            {
                max = max is null ? segmentMax : Math.Max(max.Value, segmentMax);
            }

            moving += (long)(segment.Points[^1].TimestampUtc - segment.Points[0].TimestampUtc).TotalSeconds;
        }

        DistanceMeters = Math.Round(distance, 1);
        ElevationGainMeters = Math.Round(gain, 1);
        ElevationLossMeters = Math.Round(loss, 1);
        MaxElevationMeters = max;
        MovingDurationSeconds = moving;
        AverageSpeedMetersPerSecond = moving > 0 ? Math.Round(distance / moving, 3) : 0;
    }

    private static Error? ValidateText(string name, string? notes) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength ? ActivityErrors.InvalidName
        : notes?.Length > MaxNotesLength ? ActivityErrors.NotesTooLong
        : null;
}

public static class ActivityErrors
{
    public static readonly Error NotFound = Error.NotFound("Activity.NotFound", "The activity does not exist.");
    public static readonly Error MissingId = Error.Validation("Activity.MissingId", "A client-generated activity id is required.");
    public static readonly Error IdTaken = Error.Conflict("Activity.IdTaken", "An activity with this id already exists.");
    public static readonly Error InvalidName = Error.Validation("Activity.InvalidName", $"Name is required and must be at most {HikeActivity.MaxNameLength} characters.");
    public static readonly Error NotesTooLong = Error.Validation("Activity.NotesTooLong", "Notes are too long.");
    public static readonly Error TooFewPoints = Error.Validation("Activity.TooFewPoints", "An activity needs at least two track points.");
    public static readonly Error TooManyPoints = Error.Validation("Activity.TooManyPoints", $"An activity can have at most {HikeActivity.MaxPoints} points.");
    public static readonly Error TimestampsOutOfOrder = Error.Validation("Activity.TimestampsOutOfOrder", "Track point timestamps must not go backwards.");
    public static readonly Error TimestampsInFuture = Error.Validation("Activity.TimestampsInFuture", "Track point timestamps are in the future.");
    public static readonly Error GpxWithoutTimestamps = Error.Validation("Activity.GpxWithoutTimestamps", "Every track point needs a <time> to import a GPX file as an activity. Import it as a trail instead.");
}
