using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Groups.Application.Dtos;
using TrailHub.Groups.Application.Interfaces.Providers;
using TrailHub.Groups.Application.Interfaces.Repository;
using TrailHub.Groups.Application.Interfaces.Service;
using TrailHub.Groups.Application.Requests;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Services;

internal sealed class LiveLocationService(
    IGroupRepository groups,
    ILiveLocationStore store,
    ILiveLocationNotifier notifier,
    ICurrentUser user,
    TimeProvider clock) : ILiveLocationService
{
    public async Task<LiveLocationDto> ShareAsync(Guid groupId, ShareLocationRequest request, CancellationToken cancellationToken)
    {
        GeoPoint.Create(request.Latitude, request.Longitude).ThrowIfFailure();

        if (!await groups.IsMemberAsync(groupId, user.UserId, cancellationToken))
        {
            throw GroupErrors.NotMember.ToException();
        }

        var location = new LiveLocationDto(
            user.UserId,
            request.Latitude,
            request.Longitude,
            request.Elevation,
            request.AccuracyMeters,
            request.HeadingDegrees,
            clock.GetUtcNow().UtcDateTime);
        await store.SetAsync(groupId, location, cancellationToken);
        await notifier.LocationUpdatedAsync(groupId, location, cancellationToken);
        return location;
    }

    public async Task StopSharingAsync(Guid groupId, CancellationToken cancellationToken)
    {
        await store.RemoveAsync(groupId, user.UserId, cancellationToken);
        await notifier.LocationRemovedAsync(groupId, user.UserId, cancellationToken);
    }

    public async Task<IReadOnlyList<LiveLocationDto>> GetLocationsAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken);
        if (group is null || !group.IsMember(user.UserId))
        {
            throw GroupErrors.NotFound.ToException();
        }

        return await store.GetAsync(group.Id, group.Members.Select(m => m.UserId).ToList(), cancellationToken);
    }
}
