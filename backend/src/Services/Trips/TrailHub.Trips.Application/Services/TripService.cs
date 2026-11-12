using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Mappings;
using TrailHub.Trips.Application.Models;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Expenses;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Services;

internal sealed class TripService(
    ITripRepository trips,
    ITripExpenseRepository expenses,
    ITripPaymentRepository payments,
    IExchangeRateService exchangeRates,
    IGroupDirectory directory,
    TripEditor editor,
    IUnitOfWork unitOfWork,
    ITripChangeNotifier notifier,
    ICurrentUser user,
    TimeProvider clock) : ITripService
{
    public async Task<IReadOnlyList<TripSummaryResponse>> GetTripsAsync(Guid? groupId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Trip> visible;
        if (groupId is { } onlyGroupId)
        {
            await GetOwnGroupAsync(onlyGroupId, cancellationToken);
            visible = await trips.GetByGroupAsync(onlyGroupId, cancellationToken);
        }
        else
        {
            var groupIds = await directory.GetGroupIdsForMemberAsync(user.UserId, cancellationToken);
            visible = await trips.GetVisibleToAsync(user.UserId, groupIds, cancellationToken);
        }

        var groups = new Dictionary<Guid, GroupInfo?>();
        foreach (var id in visible.Select(t => t.GroupId).OfType<Guid>().Distinct())
        {
            groups[id] = await directory.GetAsync(id, cancellationToken);
        }

        return visible
            .Select(t => t.ToSummaryResponse(user.UserId, t.GroupId is { } id ? groups[id] : null))
            .ToList();
    }

    public async Task<TripResponse> GetTripAsync(Guid id, CancellationToken cancellationToken)
    {
        var (trip, group) = await editor.LoadVisibleAsync(id, cancellationToken);
        return await editor.ToResponseAsync(trip, group, cancellationToken);
    }

    public async Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken)
    {
        var group = request.GroupId is { } groupId ? await GetOwnGroupAsync(groupId, cancellationToken) : null;

        var trip = Trip.Create(user.UserId, request.Name, request.Description, request.StartDate, request.EndDate, clock.GetUtcNow().UtcDateTime).GetValueOrThrow();
        if (group is not null)
        {
            trip.LinkGroup(group.Id);
        }

        trips.Add(trip);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return trip.ToResponse(user.UserId, group, new ExpenseSummaryResponse(0, 0, 0));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var trip = await GetOwnTripAsync(id, cancellationToken);
        trips.Remove(trip);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(trip.Id, cancellationToken);
    }

    public async Task<TripResponse> ShareAsync(Guid id, TripGroupRequest request, CancellationToken cancellationToken)
    {
        var trip = await GetOwnTripAsync(id, cancellationToken);

        GroupInfo? group = null;
        if (request.GroupId is { } groupId)
        {
            group = await GetOwnGroupAsync(groupId, cancellationToken);
            trip.LinkGroup(groupId).ThrowIfFailure();
        }
        else
        {
            trip.UnlinkGroup().ThrowIfFailure();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(trip.Id, cancellationToken);
        return await editor.ToResponseAsync(trip, group, cancellationToken);
    }

    public async Task<TripResponse> SetCurrencyAsync(Guid id, TripCurrencyRequest request, CancellationToken cancellationToken)
    {
        var (trip, group) = await editor.LoadVisibleAsync(id, cancellationToken);
        if (trip.OwnerId != user.UserId)
        {
            throw TripErrors.NotOwner.ToException();
        }

        // Supported: what the rate provider knows, plus the trip's current currency.
        var code = CurrencyCode.Normalize(request.Currency);
        if (code is null || (code != trip.Currency && !(await exchangeRates.GetCurrenciesAsync(cancellationToken)).Any(c => c.Code == code)))
        {
            throw TripErrors.InvalidCurrency.ToException();
        }

        var ledgerIsEmpty = !await expenses.AnyForTripAsync(trip.Id, cancellationToken) && !await payments.AnyForTripAsync(trip.Id, cancellationToken);
        trip.SetCurrency(code, ledgerIsEmpty).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(trip.Id, cancellationToken);
        return await editor.ToResponseAsync(trip, group, cancellationToken);
    }

    public Task<TripResponse> AttachRouteAsync(Guid id, TripRouteRequest request, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.AttachRoute(request.RouteId), cancellationToken);

    public Task<TripResponse> UpdateDetailsAsync(Guid id, TripDetailsRequest request, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.UpdateDetails(request.Name, request.Description), cancellationToken);

    public Task<TripResponse> RescheduleAsync(Guid id, TripDatesRequest request, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.Reschedule(request.StartDate, request.EndDate), cancellationToken);

    public Task<TripResponse> UpdateDayAsync(Guid id, int dayNumber, TripDayRequest request, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.UpdateDay(dayNumber, request.Notes, request.Overnight), cancellationToken);

    public Task<TripResponse> AddTrailAsync(Guid id, int dayNumber, Guid trailId, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.AddTrail(dayNumber, trailId), cancellationToken);

    public Task<TripResponse> RemoveTrailAsync(Guid id, int dayNumber, Guid trailId, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.RemoveTrail(dayNumber, trailId), cancellationToken);

    public Task<TripResponse> CompleteAsync(Guid id, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.Complete(), cancellationToken);

    public Task<TripResponse> CancelAsync(Guid id, CancellationToken cancellationToken) =>
        editor.ChangePlanAsync(id, trip => trip.Cancel(), cancellationToken);

    /// <summary>A trip the caller can see and owns.</summary>
    private async Task<Trip> GetOwnTripAsync(Guid id, CancellationToken cancellationToken)
    {
        var (trip, _) = await editor.LoadVisibleAsync(id, cancellationToken);
        return trip.OwnerId == user.UserId ? trip : throw TripErrors.NotOwner.ToException();
    }

    /// <summary>A group the caller is a member of. Other groups are reported as not found.</summary>
    private async Task<GroupInfo> GetOwnGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await directory.GetAsync(groupId, cancellationToken);
        return group is not null && group.IsMember(user.UserId) ? group : throw TripErrors.GroupNotFound.ToException();
    }
}
