using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TrailHub.BuildingBlocks.Web.Authentication;

/// <summary>
/// Local-only authentication: <c>Authorization: Dev &lt;user-id&gt;</c> (or <c>?access_token=&lt;user-id&gt;</c>
/// on hub connections) signs the request in as that user. Never enabled outside Development.
/// </summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";
    private const string Prefix = "Dev ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? userId = null;
        var header = Request.Headers.Authorization.ToString();
        var isHub = Request.Path.StartsWithSegments(AuthenticationExtensions.HubPathPrefix);
        if (header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            userId = header[Prefix.Length..].Trim();
        }
        else if (isHub && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            // SignalR clients send accessTokenFactory's value as a bearer token on negotiate.
            userId = header["Bearer ".Length..].Trim();
        }
        else if (isHub)
        {
            userId = Request.Query["access_token"];
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim("sub", userId), new Claim("name", userId)],
            SchemeName,
            nameType: "sub",
            roleType: "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
