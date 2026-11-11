namespace TrailHub.Routes.Infrastructure.Providers.Overpass;

internal sealed record OverpassElement(string Type, long Id, double? Lat, double? Lon, OverpassCenter? Center, Dictionary<string, string>? Tags);
