namespace TrailHub.Routes.Infrastructure.Providers.Overpass;

/// <summary>Overpass servers to try in order (<c>Routing:Overpass:Servers</c>).</summary>
internal sealed record OverpassOptions(IReadOnlyList<Uri> Servers);
