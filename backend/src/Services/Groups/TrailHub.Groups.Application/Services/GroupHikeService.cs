using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Contracts;
using TrailHub.Groups.Application.Interfaces.Repository;
using TrailHub.Groups.Application.Interfaces.Service;
using TrailHub.Groups.Application.Mappings;
using TrailHub.Groups.Application.Requests;
using TrailHub.Groups.Application.Responses;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Services;

internal sealed class GroupHikeService(
    IGroupRepository groups,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    ICurrentUser user,
    TimeProvider clock) : IGroupHikeService
{
    public async Task<GroupHikeResponse> ScheduleAsync(Guid groupId, ScheduleHikeRequest request, CancellationToken cancellationToken)
    {
        GeoPoint? meetingPoint = null;
        if (request.MeetingLatitude is { } lat && request.MeetingLongitude is { } lon)
        {
            meetingPoint = GeoPoint.Create(lat, lon).GetValueOrThrow();
        }

        var group = await groups.GetByIdAsync(groupId, cancellationToken) ?? throw GroupErrors.NotFound.ToException();

        var scheduledAt = DateTime.SpecifyKind(request.ScheduledAtUtc, DateTimeKind.Utc);
        var hike = group.ScheduleHike(user.UserId, request.Title, request.TrailId, scheduledAt, meetingPoint, request.Notes, clock.GetUtcNow().UtcDateTime)
            .GetValueOrThrow();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await events.PublishAsync(
            new GroupHikeScheduledIntegrationEvent(group.Id, hike.Id, hike.Title, hike.ScheduledAtUtc, group.Members.Select(m => m.UserId).ToList()),
            cancellationToken);
        return hike.ToResponse();
    }

    public async Task CancelAsync(Guid groupId, Guid hikeId, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken) ?? throw GroupErrors.NotFound.ToException();
        group.CancelHike(user.UserId, hikeId).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<GroupHikeResponse> RsvpAsync(Guid groupId, Guid hikeId, RsvpRequest request, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken);
        if (group is null || !group.IsMember(user.UserId))
        {
            throw GroupErrors.NotFound.ToException();
        }

        group.Rsvp(user.UserId, hikeId, request.Status, clock.GetUtcNow().UtcDateTime).ThrowIfFailure();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group.Hikes.First(h => h.Id == hikeId).ToResponse();
    }
}
