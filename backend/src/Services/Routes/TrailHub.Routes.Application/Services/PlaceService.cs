using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Routes.Application.Interfaces.Providers;
using TrailHub.Routes.Application.Interfaces.Service;
using TrailHub.Routes.Application.Models;
using TrailHub.Routes.Application.Requests;
using TrailHub.Routes.Application.Responses;
using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Services;

internal sealed class PlaceService(IPoiSource pois, IGeocoder geocoder) : IPlaceService
{
    public const double MaxCorridorMeters = 2000;

    public async Task<IReadOnlyList<Poi>> GetPoisAlongAsync(PoisAlongRequest request, CancellationToken cancellationToken)
    {
        var line = request.Line.Where(p => p.Length >= 2).Select(p => GeoPoint.Create(p[0], p[1])).Where(p => p.IsSuccess).Select(p => p.Value).ToList();
        if (line.Count == 0)
        {
            throw RouteErrors.InvalidWaypoint.ToException();
        }

        var kinds = request.Kinds is { Count: > 0 } k ? k : Enum.GetValues<PoiKind>();
        return (await pois.AlongAsync(line, Math.Clamp(request.CorridorMeters ?? 500, 50, MaxCorridorMeters), kinds, cancellationToken)).GetValueOrThrow();
    }

    public async Task<IReadOnlyList<Place>> SearchPlacesAsync(string text, double? nearLatitude, double? nearLongitude, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200)
        {
            throw RouteErrors.PlaceNotFound.ToException();
        }

        GeoPoint? near = (nearLatitude, nearLongitude) is ({ } lat, { } lon) && GeoPoint.Create(lat, lon) is { IsSuccess: true } p ? p.Value : null;
        return (await geocoder.SearchAsync(text.Trim(), near, cancellationToken)).GetValueOrThrow();
    }

    public async Task<RouteNameResponse> SuggestNameAsync(IReadOnlyList<WaypointRequest> waypoints, CancellationToken cancellationToken)
    {
        if (waypoints.Count < 2)
        {
            throw RouteErrors.WaypointCount.ToException();
        }

        async Task<string?> NameOf(WaypointRequest w) =>
            w.Name ?? (await geocoder.ReverseAsync(new GeoPoint(w.Latitude, w.Longitude), cancellationToken))?.Name;

        var (first, last) = (waypoints[0], waypoints[^1]);
        var start = await NameOf(first);
        var loop = RouteMetrics.HaversineMeters(new GeoPoint(first.Latitude, first.Longitude), new GeoPoint(last.Latitude, last.Longitude)) < 300;
        if (loop)
        {
            return new RouteNameResponse(start is null ? "Loop" : $"Loop from {start}");
        }

        var end = await NameOf(last);
        return new RouteNameResponse((start, end) switch
        {
            (null, null) => "New route",
            ({ } s, null) => $"From {s}",
            (null, { } e) => $"To {e}",
            var (s, e) when s == e => $"Around {s}",
            var (s, e) => $"{s} → {e}",
        });
    }
}
