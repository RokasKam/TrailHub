using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Application.Mappings;
using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Services;

/// <summary>
/// Every trip edit has the same shape: load a trip the caller can see, check they may make this change,
/// apply one domain operation, save, tell other viewers, and return the updated trip.
/// </summary>
internal sealed class TripEditor(ITripRepository trips, IGroupDirectory directory, IUnitOfWork unitOfWork, ITripChangeNotifier notifier, ICurrentUser user)
{
    /// <summary>
    /// Loads a trip the user may see: their own, or one shared with a group they're in.
    /// Anything else is reported as missing so trip ids can't be probed.
    /// </summary>
    public async Task<(Trip Trip, GroupInfo? Group)> LoadVisibleAsync(Guid tripId, CancellationToken cancellationToken)
    {
        if (await trips.GetByIdAsync(tripId, cancellationToken) is { } trip)
        {
            var group = trip.GroupId is { } groupId ? await directory.GetAsync(groupId, cancellationToken) : null;
            if (trip.OwnerId == user.UserId || group?.IsMember(user.UserId) == true)
            {
                return (trip, group);
            }
        }

        throw TripErrors.NotFound.ToException();
    }

    /// <summary>A change to the plan, which is the owner's.</summary>
    public Task<TripResponse> ChangePlanAsync(Guid tripId, Func<Trip, Result> apply, CancellationToken cancellationToken) =>
        ChangeAsync(tripId, ownerOnly: true, (trip, _) => apply(trip), cancellationToken);

    /// <summary>A change to the checklist, which is open to everyone the trip is shared with.</summary>
    public Task<TripResponse> ChangeChecklistAsync(Guid tripId, Func<Trip, string, Result> apply, CancellationToken cancellationToken) =>
        ChangeAsync(tripId, ownerOnly: false, apply, cancellationToken);

    private async Task<TripResponse> ChangeAsync(Guid tripId, bool ownerOnly, Func<Trip, string, Result> apply, CancellationToken cancellationToken)
    {
        var (trip, group) = await LoadVisibleAsync(tripId, cancellationToken);
        if (ownerOnly && trip.OwnerId != user.UserId)
        {
            throw TripErrors.NotOwner.ToException();
        }

        apply(trip, user.UserId).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(trip.Id, cancellationToken);
        return trip.ToResponse(user.UserId, group);
    }
}
