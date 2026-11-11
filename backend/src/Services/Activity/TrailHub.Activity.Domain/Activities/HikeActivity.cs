using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Domain.Activities;

/// <summary>
/// A completed hike. Recording happens on the device (so it works offline); the finished activity
/// is uploaded once with a client-generated id, which makes retries after a lost connection safe.
/// </summary>
public sealed class HikeActivity : AggregateRoot<Guid>
{
    public const int MaxNameLength = 200;
    public const int MaxNotesLength = 4000;
    public const int MaxPoints = 100_000;
    public const int MaxShareGroups = 10;
    public const int MaxCaptionLength = 500;
    public const int MaxPhotos = 6;
    public const long MaxPhotoBytes = 10 * 1024 * 1024;

    /// <summary>Uploaded photos are stored under this prefix in the photo store.</summary>
    public const string PhotoKeyRoot = "hikes/";

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

    /// <summary>The groups whose members see this hike in their feed. Empty while not shared.</summary>
    public Guid[] SharedGroupIds { get; private set; } = [];

    /// <summary>Caption shown with the shared hike.</summary>
    public string? Caption { get; private set; }

    public TrackPrivacy Privacy { get; private set; } = TrackPrivacy.Full;

    /// <summary>When the hike was first shared; kept when sharing is edited, cleared when it's unshared.</summary>
    public DateTime? SharedAtUtc { get; private set; }

    public IReadOnlyList<HikePhoto> Photos { get; private set; } = [];

    public IEnumerable<TrackPoint> AllPoints => Segments.SelectMany(s => s.Points);

    public bool IsShared => SharedAtUtc is not null;

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

    /// <summary>
    /// Shares the hike with some of the author's groups, or updates the groups, caption and privacy of a hike that is
    /// already shared (keeping when it was first shared).
    /// </summary>
    /// <param name="groupIds">The groups to share with (1 to <see cref="MaxShareGroups"/>).</param>
    /// <param name="authorGroupIds">The groups the author currently belongs to.</param>
    /// <param name="caption">Optional caption, at most <see cref="MaxCaptionLength"/> characters.</param>
    /// <param name="privacy">What members see of the track.</param>
    /// <param name="nowUtc">The current time.</param>
    public Result Share(IReadOnlyCollection<Guid> groupIds, IReadOnlyCollection<Guid> authorGroupIds, string? caption, TrackPrivacy privacy, DateTime nowUtc)
    {
        var distinct = groupIds.Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return ActivityErrors.NoGroups;
        }

        if (distinct.Length > MaxShareGroups)
        {
            return ActivityErrors.TooManyGroups;
        }

        if (distinct.Any(g => !authorGroupIds.Contains(g)))
        {
            return ActivityErrors.NotAGroupMember;
        }

        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (trimmed?.Length > MaxCaptionLength)
        {
            return ActivityErrors.InvalidCaption;
        }

        if (!Enum.IsDefined(privacy))
        {
            return ActivityErrors.InvalidPrivacy;
        }

        SharedGroupIds = distinct;
        Caption = trimmed;
        Privacy = privacy;
        // Postgres keeps microseconds; dropping the last tick digit now means the time returned here equals the stored one.
        SharedAtUtc ??= new DateTime(nowUtc.Ticks - (nowUtc.Ticks % 10), DateTimeKind.Utc);
        return Result.Success();
    }

    /// <summary>Stops sharing. Likes and comments are kept and reappear if the hike is shared again.</summary>
    public void Unshare()
    {
        SharedGroupIds = [];
        Caption = null;
        Privacy = TrackPrivacy.Full;
        SharedAtUtc = null;
    }

    /// <summary>Whether <paramref name="userId"/> can see this hike in the feed: it's theirs, or shared with a group they're in.</summary>
    public bool IsVisibleTo(string userId, IReadOnlyCollection<Guid> userGroupIds) =>
        IsShared && (UserId == userId || SharedGroupIds.Any(userGroupIds.Contains));

    /// <summary>The storage key prefix for this hike's photos, so one hike can't claim another's files.</summary>
    public static string PhotoKeyPrefix(Guid activityId) => $"{PhotoKeyRoot}{activityId:N}/";

    /// <summary>Attaches an uploaded photo stored under <see cref="PhotoKeyPrefix"/>.</summary>
    public Result<HikePhoto> AddPhoto(string storageKey, string? caption, DateTime nowUtc)
    {
        if (Photos.Count >= MaxPhotos)
        {
            return ActivityErrors.TooManyPhotos;
        }

        var trimmed = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (trimmed?.Length > MaxCaptionLength)
        {
            return ActivityErrors.InvalidCaption;
        }

        if (!storageKey.StartsWith(PhotoKeyPrefix(Id), StringComparison.Ordinal) || storageKey.Contains("..", StringComparison.Ordinal))
        {
            return ActivityErrors.InvalidPhotoKey;
        }

        var photo = new HikePhoto(Guid.CreateVersion7(), storageKey, trimmed, nowUtc);
        Photos = [.. Photos, photo];
        return photo;
    }

    /// <summary>Detaches a photo and returns it, so its stored file can be deleted.</summary>
    public Result<HikePhoto> RemovePhoto(Guid photoId)
    {
        if (Photos.FirstOrDefault(p => p.Id == photoId) is not { } photo)
        {
            return ActivityErrors.PhotoNotFound;
        }

        Photos = Photos.Where(p => p.Id != photoId).ToList();
        return photo;
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
