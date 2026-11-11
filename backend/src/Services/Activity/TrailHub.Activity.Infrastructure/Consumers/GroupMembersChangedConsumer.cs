using MassTransit;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Models;
using TrailHub.Contracts;

namespace TrailHub.Activity.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class GroupMembersChangedConsumer(IGroupMembershipService groupMembership) : IConsumer<GroupMembersChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<GroupMembersChangedIntegrationEvent> context)
    {
        var e = context.Message;
        return groupMembership.SyncAsync(
            e.GroupId, e.Name, e.Members.Select(m => new GroupMemberInfo(m.UserId, m.DisplayName)).ToList(), e.Deleted, e.OccurredAtUtc, context.CancellationToken);
    }
}
