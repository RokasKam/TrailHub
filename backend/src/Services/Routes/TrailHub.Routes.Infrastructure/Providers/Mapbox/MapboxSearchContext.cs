namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

internal sealed record MapboxSearchContext(MapboxSearchNamedPart? Locality, MapboxSearchNamedPart? Place, MapboxSearchNamedPart? Region);
