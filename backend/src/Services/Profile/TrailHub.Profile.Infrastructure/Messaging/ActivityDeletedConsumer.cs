using MassTransit;
using TrailHub.Contracts;
using TrailHub.Profile.Application.Interfaces.Service;

namespace TrailHub.Profile.Infrastructure.Messaging;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class ActivityDeletedConsumer(IProfileHikeService profileHikeService) : IConsumer<ActivityDeletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ActivityDeletedIntegrationEvent> context) =>
        profileHikeService.RemoveHikeAsync(context.Message.UserId, context.Message.ActivityId, context.CancellationToken);
}
