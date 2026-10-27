using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.BuildingBlocks.Web.Authentication;

/// <summary>
/// Auth0 access tokens carry only <c>sub</c> by default. A post-login Action adds the user's email and
/// name as namespaced custom claims (<c>Auth0:ClaimsNamespace</c>, e.g. <c>https://trailhub.app/email</c>);
/// plain <c>email</c>/<c>name</c> claims are used when present (e.g. development auth).
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor, IConfiguration configuration) : ICurrentUser
{
    private readonly string _namespace = configuration["Auth0:ClaimsNamespace"] ?? "https://trailhub.app/";

    public string UserId =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? throw new InvalidOperationException("There is no authenticated user for this request.");

    public string? Email => Claim("email");

    public string? Name => Claim("name");

    private string? Claim(string name)
    {
        var user = accessor.HttpContext?.User;
        return user?.FindFirst(_namespace + name)?.Value ?? user?.FindFirst(name)?.Value;
    }
}
