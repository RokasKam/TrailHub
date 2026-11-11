using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Trails.Domain.Trails;

public sealed class Trail : AggregateRoot<Guid>
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 5000;
    public const int MaxRegionLength = 200;
    public const int MaxRoutePoints = 50_000;

    /// <summary>Distinct reports that take a trail out of Discover until a moderator looks at it.</summary>
    public const int AutoHideReportCount = 3;
    public const int MaxPointsOfInterest = 200;
    public const int MaxPhotos = 50;

    /// <summary>Start and end closer than this make the trail a loop.</summary>
    private const double LoopThresholdMeters = 250;

    private readonly List<PointOfInterest> _pointsOfInterest = [];
    private readonly List<TrailPhoto> _photos = [];

    private Trail()
    {
    }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>Human-readable location, e.g. "High Tatras, Slovakia". Used for text search.</summary>
    public string Region { get; private set; } = null!;

    public Difficulty Difficulty { get; private set; }

    public IReadOnlyList<GeoPoint> Route { get; private set; } = [];

    public double DistanceMeters { get; private set; }

    public double ElevationGainMeters { get; private set; }

    public double ElevationLossMeters { get; private set; }

    public double? MaxElevationMeters { get; private set; }

    public int EstimatedDurationMinutes { get; private set; }

    public bool IsLoop { get; private set; }

    public double AverageRating { get; private set; }

    public int ReviewCount { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Set when the trail is out of Discover; hidden trails are only visible to their author and moderators.</summary>
    public HiddenBy? HiddenBy { get; private set; }

    public DateTime? HiddenAtUtc { get; private set; }

    public bool IsHidden => HiddenBy is not null;

    public IReadOnlyList<PointOfInterest> PointsOfInterest => _pointsOfInterest;

    public IReadOnlyList<TrailPhoto> Photos => _photos;

    public GeoPoint Start => Route[0];

    public GeoBounds Bounds => GeoBounds.From(Route);

    public static Result<Trail> Create(
        string name,
        string? description,
        string region,
        Difficulty difficulty,
        IReadOnlyList<GeoPoint> route,
        string createdBy,
        DateTime nowUtc,
        Guid? id = null)
    {
        if (ValidateDetails(name, description, region) is { } error)
        {
            return error;
        }

        if (route.Count < 2)
        {
            return TrailErrors.RouteTooShort;
        }

        if (route.Count > MaxRoutePoints)
        {
            return TrailErrors.RouteTooLong;
        }

        var trail = new Trail
        {
            Id = id ?? Guid.CreateVersion7(),
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Region = region.Trim(),
            Difficulty = difficulty,
            CreatedBy = createdBy,
            CreatedAtUtc = nowUtc,
        };
        trail.SetRoute(route);
        return trail;
    }

    public Result UpdateDetails(string name, string? description, string region, Difficulty difficulty, string userId)
    {
        if (userId != CreatedBy)
        {
            return TrailErrors.NotOwner;
        }

        if (ValidateDetails(name, description, region) is { } error)
        {
            return error;
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Region = region.Trim();
        Difficulty = difficulty;
        return Result.Success();
    }

    public void Hide(HiddenBy by, DateTime nowUtc)
    {
        // A moderator's decision isn't overridden by later, weaker ones (e.g. reports after a moderator hid it).
        if (HiddenBy == Trails.HiddenBy.Moderator && by != Trails.HiddenBy.Moderator)
        {
            return;
        }

        HiddenBy = by;
        HiddenAtUtc = nowUtc;
    }

    /// <summary>Back in Discover. The author can only undo their own unpublish, not a moderator's or the reports' decision.</summary>
    public Result Unhide(HiddenBy by)
    {
        if (HiddenBy is null)
        {
            return Result.Success();
        }

        if (by == Trails.HiddenBy.Author && HiddenBy != Trails.HiddenBy.Author)
        {
            return TrailErrors.HiddenByModerators;
        }

        HiddenBy = null;
        HiddenAtUtc = null;
        return Result.Success();
    }

    /// <summary>Replaces the line, e.g. when the planned route a trail was published from is published again.</summary>
    public Result ReplaceRoute(IReadOnlyList<GeoPoint> route, string userId)
    {
        if (userId != CreatedBy)
        {
            return TrailErrors.NotOwner;
        }

        if (route.Count < 2)
        {
            return TrailErrors.RouteTooShort;
        }

        if (route.Count > MaxRoutePoints)
        {
            return TrailErrors.RouteTooLong;
        }

        SetRoute(route);
        return Result.Success();
    }

    public Result<PointOfInterest> AddPointOfInterest(string name, PointOfInterestType type, GeoPoint location, string? description)
    {
        if (_pointsOfInterest.Count >= MaxPointsOfInterest)
        {
            return TrailErrors.TooManyPointsOfInterest;
        }

        var poi = PointOfInterest.Create(name, type, location, description);
        if (poi.IsFailure)
        {
            return poi.Error!;
        }

        _pointsOfInterest.Add(poi.Value);
        return poi.Value;
    }

    public Result RemovePointOfInterest(Guid pointOfInterestId, string userId)
    {
        if (userId != CreatedBy)
        {
            return TrailErrors.NotOwner;
        }

        return _pointsOfInterest.RemoveAll(p => p.Id == pointOfInterestId) > 0
            ? Result.Success()
            : TrailErrors.PointOfInterestNotFound;
    }

    public Result AddPhoto(string url, string? caption, string uploadedBy, DateTime nowUtc)
    {
        if (_photos.Count >= MaxPhotos)
        {
            return TrailErrors.TooManyPhotos;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return TrailErrors.InvalidPhotoUrl;
        }

        _photos.Add(new TrailPhoto(uri.ToString(), string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(), uploadedBy, nowUtc));
        return Result.Success();
    }

    /// <summary>
    /// Adds a photo uploaded to TrailHub's own storage. <paramref name="storageKey"/> must live under
    /// this trail's prefix (see <see cref="PhotoKeyPrefix"/>), so one trail can't claim another's files.
    /// </summary>
    public Result AddUploadedPhoto(string storageKey, string? caption, string uploadedBy, DateTime nowUtc)
    {
        if (_photos.Count >= MaxPhotos)
        {
            return TrailErrors.TooManyPhotos;
        }

        if (!storageKey.StartsWith(PhotoKeyPrefix(Id), StringComparison.Ordinal) || storageKey.Contains("..", StringComparison.Ordinal))
        {
            return TrailErrors.InvalidPhotoUrl;
        }

        _photos.Add(new TrailPhoto(storageKey, string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(), uploadedBy, nowUtc));
        return Result.Success();
    }

    public static string PhotoKeyPrefix(Guid trailId) => $"trails/{trailId:N}/";

    /// <summary>Denormalized from the trail's reviews so search results can sort by rating cheaply.</summary>
    public void UpdateRatingSummary(double averageRating, int reviewCount)
    {
        AverageRating = reviewCount == 0 ? 0 : Math.Round(averageRating, 2);
        ReviewCount = reviewCount;
    }

    public IReadOnlyList<ElevationSample> GetElevationProfile(int maxSamples) => ElevationProfile.Build(Route, maxSamples);

    /// <summary>
    /// Naismith's rule: 5 km/h on the flat plus one hour per 600 m of ascent. A simple, well-known
    /// baseline; it doesn't account for terrain or fitness.
    /// </summary>
    public static int EstimateDurationMinutes(double distanceMeters, double elevationGainMeters) =>
        (int)Math.Round(distanceMeters / 5000 * 60 + elevationGainMeters / 600 * 60);

    private void SetRoute(IReadOnlyList<GeoPoint> route)
    {
        var metrics = RouteMetrics.Calculate(route);
        Route = route.ToList();
        DistanceMeters = Math.Round(metrics.DistanceMeters, 1);
        ElevationGainMeters = Math.Round(metrics.ElevationGainMeters, 1);
        ElevationLossMeters = Math.Round(metrics.ElevationLossMeters, 1);
        MaxElevationMeters = metrics.MaxElevationMeters;
        EstimatedDurationMinutes = EstimateDurationMinutes(metrics.DistanceMeters, metrics.ElevationGainMeters);
        IsLoop = RouteMetrics.HaversineMeters(route[0], route[^1]) <= LoopThresholdMeters;
    }

    private static Error? ValidateDetails(string name, string? description, string region)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            return TrailErrors.InvalidName;
        }

        if (description?.Length > MaxDescriptionLength)
        {
            return TrailErrors.DescriptionTooLong;
        }

        if (string.IsNullOrWhiteSpace(region) || region.Trim().Length > MaxRegionLength)
        {
            return TrailErrors.InvalidRegion;
        }

        return null;
    }
}
