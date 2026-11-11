using TrailHub.Groups.Application.Requests;
using TrailHub.Groups.Application.Responses;

namespace TrailHub.Groups.Application.Interfaces.Service;

/// <summary>Scheduled group hikes, their meeting points and RSVPs.</summary>
public interface IGroupHikeService
{
    /// <summary>Schedules a group hike (any member); the organizer is marked as going.</summary>
    Task<GroupHikeResponse> ScheduleAsync(Guid groupId, ScheduleHikeRequest request, CancellationToken cancellationToken);

    /// <summary>Cancels a group hike (its organizer or the owner).</summary>
    Task CancelAsync(Guid groupId, Guid hikeId, CancellationToken cancellationToken);

    /// <summary>Answers a scheduled hike: Going, Maybe or NotGoing (replaces any previous answer).</summary>
    Task<GroupHikeResponse> RsvpAsync(Guid groupId, Guid hikeId, RsvpRequest request, CancellationToken cancellationToken);
}
