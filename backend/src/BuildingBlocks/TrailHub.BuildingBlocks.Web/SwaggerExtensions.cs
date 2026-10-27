using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

namespace TrailHub.BuildingBlocks.Web;

public static class SwaggerExtensions
{
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// Swagger document for the service's controllers, with XML doc comments and an "Authorize"
    /// button that takes an Auth0 access token (or a development user id in Development auth mode).
    /// </summary>
    public static IServiceCollection AddTrailHubSwagger(this IServiceCollection services, string title)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = title, Version = "v1" });

            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetEntryAssembly()!.GetName().Name}.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml);
            }

            // Nested DTOs share short names across namespaces (e.g. two "Request" types); full names avoid clashes.
            options.CustomSchemaIds(type => type.FullName!.Replace('+', '.'));

            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Auth0 access token (audience = the TrailHub API identifier).",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
            });
        });
        return services;
    }

    /// <summary>Serves <c>/swagger/v1/swagger.json</c> and the UI at <c>/swagger</c> (Development, or when <c>Swagger:Enabled</c>).</summary>
    public static WebApplication UseTrailHubSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() && !app.Configuration.GetValue("Swagger:Enabled", false))
        {
            return app;
        }

        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("v1/swagger.json", "v1"));
        return app;
    }
}
