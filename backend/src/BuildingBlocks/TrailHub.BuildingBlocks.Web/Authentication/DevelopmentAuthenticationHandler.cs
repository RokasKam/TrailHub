using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TrailHub.BuildingBlocks.Web.Authentication;

/// <summary>
/// Local-only authentication: <c>Authorization: Dev &lt;user-id&gt;</c> (or <c>?access_token=&lt;user-id&gt;</c>
/// on hub connections) signs the request in as that user. Its <c>name</c> claim is the user id, or the optional
/// <c>X-Dev-Name</c> header; the optional <c>X-Dev-Email</c> header adds an <c>email</c> claim (like an Auth0
/// email/password user, whose name is their email). Never enabled outside Development.
/// </summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";
    private const string Prefix = "Dev ";
    private const string NameHeader = "X-Dev-Name";
    private const string EmailHeader = "X-Dev-Email";

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

        var name = Request.Headers[NameHeader].ToString().Trim();
        var email = Request.Headers[EmailHeader].ToString().Trim();
        List<Claim> claims = [new("sub", userId), new("name", name.Length > 0 ? name : userId)];
        if (email.Length > 0)
        {
            claims.Add(new Claim("email", email));
        }

        var identity = new ClaimsIdentity(
            claims,
            SchemeName,
            nameType: "sub",
            roleType: "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
