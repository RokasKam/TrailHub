using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Routes;

/// <summary>Finds walkable lines between points (OpenRouteService's hiking profile in production).</summary>
public interface IRouter
{
    /// <summary>A route through all waypoints, in order, following hiking paths.</summary>
    Task<Result<RoutedTrack>> RouteAsync(IReadOnlyList<GeoPoint> waypoints, CancellationToken cancellationToken);

    /// <summary>Up to <paramref name="count"/> different routes between two points, best first.</summary>
    Task<Result<IReadOnlyList<RoutedTrack>>> AlternativesAsync(GeoPoint from, GeoPoint to, int count, CancellationToken cancellationToken);
}

public sealed record Poi(string Id, string Name, PoiKind Kind, double Latitude, double Longitude, double? Elevation);

/// <summary>Water, campsites, huts, hotels and viewpoints from OpenStreetMap.</summary>
public interface IPoiSource
{
    /// <summary>Points of interest within <paramref name="corridorMeters"/> of the line.</summary>
    Task<Result<IReadOnlyList<Poi>>> AlongAsync(IReadOnlyList<GeoPoint> line, double corridorMeters, IReadOnlyCollection<PoiKind> kinds, CancellationToken cancellationToken);
}

public sealed record Place(string Name, string? Region, double Latitude, double Longitude);

public interface IGeocoder
{
    Task<Result<IReadOnlyList<Place>>> SearchAsync(string text, GeoPoint? near, CancellationToken cancellationToken);
}

/// <param name="IsOwner">False for routes other people shared with one of your groups.</param>
public sealed record RouteSummary(
    Guid Id,
    string Name,
    double DistanceMeters,
    double AscentMeters,
    int DurationMinutes,
    int DayCount,
    bool Snapped,
    double StartLatitude,
    double StartLongitude,
    DateTime UpdatedAtUtc,
    string OwnerId,
    bool IsOwner,
    bool Published);

public interface IRouteRepository
{
    Task<PlannedRoute?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The user's own routes and routes shared with any of <paramref name="groupIds"/>.</summary>
    Task<IReadOnlyList<RouteSummary>> GetSummariesVisibleToAsync(string userId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken);

    void Add(PlannedRoute route);

    void Remove(PlannedRoute route);
}
