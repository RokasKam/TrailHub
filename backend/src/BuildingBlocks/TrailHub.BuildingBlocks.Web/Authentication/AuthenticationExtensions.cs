using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace TrailHub.BuildingBlocks.Web.Authentication;

public static class AuthenticationExtensions
{
    /// <summary>Paths whose WebSocket clients send the token as <c>?access_token=</c> (browsers can't set headers there).</summary>
    public const string HubPathPrefix = "/hubs";

    /// <summary>
    /// Validates Auth0 access tokens (section <c>Auth0</c>: <c>Domain</c>, <c>Audience</c>).
    /// Login, registration and refresh-token rotation happen between the app and Auth0;
    /// services only ever see short-lived access tokens.
    /// <para>
    /// With <c>Auth:Mode=Development</c> (only honored in the Development environment) the
    /// <see cref="DevelopmentAuthenticationHandler"/> is used instead, so the stack runs without an Auth0 tenant.
    /// </para>
    /// </summary>
    public static WebApplicationBuilder AddTrailHubAuthentication(this WebApplicationBuilder builder)
    {
        var useDevelopmentAuth = string.Equals(builder.Configuration["Auth:Mode"], "Development", StringComparison.OrdinalIgnoreCase);
        if (useDevelopmentAuth && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Auth:Mode=Development is only allowed in the Development environment.");
        }

        if (useDevelopmentAuth)
        {
            builder.Services
                .AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, null);
        }
        else
        {
            var domain = builder.Configuration["Auth0:Domain"];
            var audience = builder.Configuration["Auth0:Audience"];
            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException("Auth0:Domain and Auth0:Audience must be configured (or set Auth:Mode=Development locally).");
            }

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = $"https://{domain.TrimEnd('/')}/";
                    options.Audience = audience;
                    options.MapInboundClaims = false; // keep "sub" as-is instead of the long WS-* claim URI
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "sub",
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var token = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(HubPathPrefix))
                            {
                                context.Token = token;
                            }

                            return Task.CompletedTask;
                        },
                    };
                });
        }

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return builder;
    }
}
