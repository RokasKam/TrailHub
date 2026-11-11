namespace TrailHub.Routes.Infrastructure.Providers.Overpass;

internal sealed record OverpassResponse(IReadOnlyList<OverpassElement>? Elements);
