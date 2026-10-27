using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrailHub.BuildingBlocks.Application;

namespace TrailHub.BuildingBlocks.Infrastructure.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Configures MassTransit over RabbitMQ (section <c>RabbitMq</c>). When no host is configured
    /// it falls back to an in-memory bus so a single service can run without a broker.
    /// </summary>
    public static IServiceCollection AddTrailHubMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var host = configuration["RabbitMq:Host"];

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();
            configureConsumers?.Invoke(bus);

            if (string.IsNullOrWhiteSpace(host))
            {
                bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
                return;
            }

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(host, configuration["RabbitMq:VirtualHost"] ?? "/", h =>
                {
                    h.Username(configuration["RabbitMq:Username"] ?? "guest");
                    h.Password(configuration["RabbitMq:Password"] ?? "guest");
                });
                cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(2)));
                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        return services;
    }

    private sealed class MassTransitEventPublisher(IPublishEndpoint publishEndpoint) : IEventPublisher
    {
        public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : class =>
            publishEndpoint.Publish(integrationEvent, cancellationToken);
    }
}
