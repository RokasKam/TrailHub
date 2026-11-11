namespace TrailHub.Routes.Infrastructure.Providers.OpenMeteo;

internal sealed record OpenMeteoElevationResponse(IReadOnlyList<double?> Elevation);
