namespace TrailHub.Routes.Application.Models;

/// <summary>A place found by name or by location: a town, peak, hut and the like.</summary>
public sealed record Place(string Name, string? Region, double Latitude, double Longitude);
