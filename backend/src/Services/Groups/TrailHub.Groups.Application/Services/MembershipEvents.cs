using TrailHub.BuildingBlocks.Application;
using TrailHub.Contracts;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Services;

internal static class MembershipEvents
{
    /// <summary>Lets other services (Trips) keep their copy of who's in the group current.</summary>
    public static Task PublishMembersAsync(this IEventPublisher events, HikingGroup group, DateTime nowUtc, CancellationToken cancellationToken, bool deleted = false) =>
        events.PublishAsync(
            new GroupMembersChangedIntegrationEvent(
                group.Id,
                group.Name,
                deleted ? [] : group.Members.Select(m => new GroupMemberSnapshot(m.UserId, m.DisplayName)).ToList(),
                deleted,
                nowUtc),
            cancellationToken);
}
