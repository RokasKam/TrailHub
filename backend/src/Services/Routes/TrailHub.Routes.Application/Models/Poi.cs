using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Models;

/// <summary>A point of interest from the map data (water, campsite, hut, shelter, hotel or viewpoint).</summary>
public sealed record Poi(string Id, string Name, PoiKind Kind, double Latitude, double Longitude, double? Elevation);
