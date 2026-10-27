using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Groups;

public sealed record LiveLocationDto(
    string UserId,
    double Latitude,
    double Longitude,
    double? Elevation,
    double? AccuracyMeters,
    double? HeadingDegrees,
    DateTime RecordedAtUtc);

/// <summary>
/// Latest position per member. Ephemeral by design: entries expire on their own, so a phone that
/// loses signal simply drops off the map instead of showing a stale position forever.
/// </summary>
public interface ILiveLocationStore
{
    static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(15);

    Task SetAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken);

    Task<IReadOnlyList<LiveLocationDto>> GetAsync(Guid groupId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    Task RemoveAsync(Guid groupId, string userId, CancellationToken cancellationToken);
}

/// <summary>
/// Pushes live-location changes to members watching the group's map (SignalR in the API layer).
/// Used for updates from both the WebSocket hub and the REST endpoint (background updates).
/// </summary>
public interface ILiveLocationNotifier
{
    Task LocationUpdatedAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken);

    Task LocationRemovedAsync(Guid groupId, string userId, CancellationToken cancellationToken);
}

public sealed record ShareLocationCommand(
    Guid GroupId,
    double Latitude,
    double Longitude,
    double? Elevation,
    double? AccuracyMeters,
    double? HeadingDegrees) : ICommand<LiveLocationDto>;

internal sealed class ShareLocationHandler(IGroupRepository groups, ILiveLocationStore store, ILiveLocationNotifier notifier, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<ShareLocationCommand, LiveLocationDto>
{
    public async Task<Result<LiveLocationDto>> Handle(ShareLocationCommand command, CancellationToken cancellationToken)
    {
        if (GeoPoint.Create(command.Latitude, command.Longitude).Error is { } invalid)
        {
            return invalid;
        }

        if (!await groups.IsMemberAsync(command.GroupId, user.UserId, cancellationToken))
        {
            return GroupErrors.NotMember;
        }

        var location = new LiveLocationDto(
            user.UserId,
            command.Latitude,
            command.Longitude,
            command.Elevation,
            command.AccuracyMeters,
            command.HeadingDegrees,
            clock.GetUtcNow().UtcDateTime);
        await store.SetAsync(command.GroupId, location, cancellationToken);
        await notifier.LocationUpdatedAsync(command.GroupId, location, cancellationToken);
        return location;
    }
}

public sealed record StopSharingLocationCommand(Guid GroupId) : ICommand;

internal sealed class StopSharingLocationHandler(ILiveLocationStore store, ILiveLocationNotifier notifier, ICurrentUser user) : ICommandHandler<StopSharingLocationCommand>
{
    public async Task<Result> Handle(StopSharingLocationCommand command, CancellationToken cancellationToken)
    {
        await store.RemoveAsync(command.GroupId, user.UserId, cancellationToken);
        await notifier.LocationRemovedAsync(command.GroupId, user.UserId, cancellationToken);
        return Result.Success();
    }
}

public sealed record GetLiveLocationsQuery(Guid GroupId) : IQuery<IReadOnlyList<LiveLocationDto>>;

internal sealed class GetLiveLocationsHandler(IGroupRepository groups, ILiveLocationStore store, ICurrentUser user)
    : IQueryHandler<GetLiveLocationsQuery, IReadOnlyList<LiveLocationDto>>
{
    public async Task<Result<IReadOnlyList<LiveLocationDto>>> Handle(GetLiveLocationsQuery query, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(query.GroupId, cancellationToken);
        if (group is null || !group.IsMember(user.UserId))
        {
            return GroupErrors.NotFound;
        }

        return Result<IReadOnlyList<LiveLocationDto>>.Success(
            await store.GetAsync(group.Id, group.Members.Select(m => m.UserId).ToList(), cancellationToken));
    }
}
