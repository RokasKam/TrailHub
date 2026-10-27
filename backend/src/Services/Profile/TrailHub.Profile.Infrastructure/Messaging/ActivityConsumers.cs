using MassTransit;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Application;
using TrailHub.Contracts;
using TrailHub.Profile.Application.Profiles;

namespace TrailHub.Profile.Infrastructure.Messaging;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class ActivityCompletedConsumer(
    ICommandHandler<RecordCompletedHikeCommand, IReadOnlyList<string>> handler,
    ILogger<ActivityCompletedConsumer> logger)
    : IConsumer<ActivityCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ActivityCompletedIntegrationEvent> context)
    {
        var e = context.Message;
        var result = await handler.Handle(
            new RecordCompletedHikeCommand(e.UserId, e.ActivityId, e.DistanceMeters, e.ElevationGainMeters, e.CompletedAtUtc),
            context.CancellationToken);

        if (result.IsSuccess && result.Value.Count > 0)
        {
            logger.LogInformation("User {UserId} unlocked {Achievements}", e.UserId, string.Join(", ", result.Value));
        }
    }
}

internal sealed class ActivityDeletedConsumer(ICommandHandler<RemoveHikeCommand> handler) : IConsumer<ActivityDeletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ActivityDeletedIntegrationEvent> context) =>
        handler.Handle(new RemoveHikeCommand(context.Message.UserId, context.Message.ActivityId), context.CancellationToken);
}
