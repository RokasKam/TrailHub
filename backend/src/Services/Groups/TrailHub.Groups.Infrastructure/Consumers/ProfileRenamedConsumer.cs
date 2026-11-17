using MassTransit;
using TrailHub.Contracts;
using TrailHub.Groups.Application.Interfaces.Service;

namespace TrailHub.Groups.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class ProfileRenamedConsumer(IGroupService groupService) : IConsumer<ProfileRenamedIntegrationEvent>
{
    public Task Consume(ConsumeContext<ProfileRenamedIntegrationEvent> context) =>
        groupService.RenameMemberAsync(context.Message.UserId, context.Message.DisplayName, context.CancellationToken);
}
