using MassTransit;
using Microsoft.Extensions.Logging;
using TrailHub.Contracts;
using TrailHub.Profile.Application.Interfaces.Service;

namespace TrailHub.Profile.Infrastructure.Messaging;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class ActivityCompletedConsumer(
    IProfileHikeService profileHikeService,
    ILogger<ActivityCompletedConsumer> logger)
    : IConsumer<ActivityCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ActivityCompletedIntegrationEvent> context)
    {
        var e = context.Message;
        var unlocked = await profileHikeService.RecordCompletedHikeAsync(
            e.UserId, e.ActivityId, e.DistanceMeters, e.ElevationGainMeters, e.CompletedAtUtc, context.CancellationToken);

        if (unlocked.Count > 0)
        {
            logger.LogInformation("User {UserId} unlocked {Achievements}", e.UserId, string.Join(", ", unlocked));
        }
    }
}
