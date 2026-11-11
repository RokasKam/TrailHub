using MassTransit;
using TrailHub.Contracts;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Application.Requests;

namespace TrailHub.Trips.Infrastructure.Consumers;

/// <summary>Adapter from the message bus to the application layer; holds no business logic.</summary>
internal sealed class GroupMembersChangedConsumer(IGroupMembershipService groupMembership) : IConsumer<GroupMembersChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<GroupMembersChangedIntegrationEvent> context)
    {
        var e = context.Message;
        return groupMembership.SyncGroupMembersAsync(
            new SyncGroupMembersRequest(e.GroupId, e.Name, e.Members.Select(m => new GroupMemberInfo(m.UserId, m.DisplayName)).ToList(), e.Deleted, e.OccurredAtUtc),
            context.CancellationToken);
    }
}
