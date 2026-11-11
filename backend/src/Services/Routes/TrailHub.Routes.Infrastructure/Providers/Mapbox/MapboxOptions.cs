namespace TrailHub.Routes.Infrastructure.Providers.Mapbox;

/// <summary>The Mapbox access token (<c>Routing:Mapbox:AccessToken</c>): the app's public <c>pk.</c> token works.</summary>
internal sealed record MapboxOptions(string? AccessToken)
{
    public bool Configured => !string.IsNullOrWhiteSpace(AccessToken);
}
