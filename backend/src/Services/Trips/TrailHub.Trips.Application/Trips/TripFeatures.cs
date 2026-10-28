using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Trips;

public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Trips the user owns plus trips shared with any of <paramref name="groupIds"/>.</summary>
    Task<IReadOnlyList<Trip>> GetVisibleToAsync(string userId, IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<Trip>> GetByGroupAsync(Guid groupId, CancellationToken cancellationToken);

    void Add(Trip trip);

    void Remove(Trip trip);
}

public sealed record TripDayDto(int DayNumber, DateOnly Date, string? Notes, string? Overnight, IReadOnlyList<Guid> TrailIds);

/// <param name="IsDone">For personal items, whether the viewer has packed theirs; for shared items, whether the person bringing it has.</param>
public sealed record ChecklistItemDto(
    Guid Id,
    string Text,
    ChecklistItemKind Kind,
    bool IsDone,
    string? ClaimedBy,
    string? ClaimedByName,
    string AddedBy,
    bool CanRemove);

/// <param name="PersonalDone">How many personal items this member has ticked.</param>
public sealed record TripMemberDto(string UserId, string DisplayName, bool IsOwner, int PersonalDone);

/// <param name="Members">The linked group's members with their packing progress; empty for private trips.</param>
public sealed record TripDto(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    TripStatus Status,
    IReadOnlyList<TripDayDto> Days,
    IReadOnlyList<ChecklistItemDto> Checklist,
    DateTime CreatedAtUtc,
    string OwnerId,
    bool IsOwner,
    Guid? GroupId,
    string? GroupName,
    IReadOnlyList<TripMemberDto> Members);

/// <param name="SharedUnclaimed">Group gear nobody has said they'll bring yet.</param>
public sealed record TripSummaryDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    TripStatus Status,
    int TrailCount,
    Guid? GroupId,
    string? GroupName,
    bool IsOwner,
    int SharedUnclaimed);

internal static class TripMappings
{
    public static TripDto ToDto(this Trip t, string viewerId, GroupInfo? group)
    {
        var personal = t.Checklist.Where(i => i.Kind == ChecklistItemKind.Personal).ToList();
        return new TripDto(
            t.Id,
            t.Name,
            t.Description,
            t.StartDate,
            t.EndDate,
            t.DayCount,
            t.Status,
            t.Days.OrderBy(d => d.DayNumber).Select(d => new TripDayDto(d.DayNumber, t.StartDate.AddDays(d.DayNumber - 1), d.Notes, d.Overnight, d.TrailIds.ToList())).ToList(),
            t.Checklist
                .Select(i => new ChecklistItemDto(
                    i.Id,
                    i.Text,
                    i.Kind,
                    i.IsDoneFor(viewerId),
                    i.ClaimedBy,
                    i.ClaimedBy is { } claimer ? group?.NameOf(claimer) ?? "Former member" : null,
                    i.AddedBy,
                    t.CanRemoveChecklistItem(viewerId, i)))
                .ToList(),
            t.CreatedAtUtc,
            t.OwnerId,
            t.OwnerId == viewerId,
            t.GroupId,
            group?.Name,
            group is null
                ? []
                : group.Members
                    .Select(m => new TripMemberDto(m.UserId, m.DisplayName, m.UserId == t.OwnerId, personal.Count(i => i.DoneBy.Contains(m.UserId))))
                    .OrderByDescending(m => m.IsOwner)
                    .ThenBy(m => m.DisplayName)
                    .ToList());
    }

    public static TripSummaryDto ToSummary(this Trip t, string viewerId, GroupInfo? group) => new(
        t.Id,
        t.Name,
        t.StartDate,
        t.EndDate,
        t.DayCount,
        t.Status,
        t.Days.Sum(d => d.TrailIds.Count),
        t.GroupId,
        group?.Name,
        t.OwnerId == viewerId,
        t.GroupId is null ? 0 : t.Checklist.Count(i => i.Kind == ChecklistItemKind.Shared && i.ClaimedBy is null));
}

internal static class TripAccess
{
    /// <summary>
    /// Loads a trip the user may see: their own, or one shared with a group they're in.
    /// Anything else is reported as missing so trip ids can't be probed.
    /// </summary>
    public static async Task<(Trip Trip, GroupInfo? Group)?> LoadVisibleAsync(
        this ITripRepository trips, IGroupDirectory directory, Guid tripId, string userId, CancellationToken cancellationToken)
    {
        if (await trips.GetByIdAsync(tripId, cancellationToken) is not { } trip)
        {
            return null;
        }

        var group = trip.GroupId is { } groupId ? await directory.GetAsync(groupId, cancellationToken) : null;
        return trip.OwnerId == userId || group?.IsMember(userId) == true ? (trip, group) : null;
    }
}

public sealed record CreateTripCommand(string Name, string? Description, DateOnly StartDate, DateOnly EndDate, Guid? GroupId = null) : ICommand<TripDto>;

internal sealed class CreateTripHandler(ITripRepository trips, IGroupDirectory directory, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CreateTripCommand, TripDto>
{
    public async Task<Result<TripDto>> Handle(CreateTripCommand command, CancellationToken cancellationToken)
    {
        GroupInfo? group = null;
        if (command.GroupId is { } groupId)
        {
            group = await directory.GetAsync(groupId, cancellationToken);
            if (group is null || !group.IsMember(user.UserId))
            {
                return TripErrors.GroupNotFound;
            }
        }

        var trip = Trip.Create(user.UserId, command.Name, command.Description, command.StartDate, command.EndDate, clock.GetUtcNow().UtcDateTime);
        if (trip.IsFailure)
        {
            return trip.Error!;
        }

        if (group is not null)
        {
            trip.Value.LinkGroup(group.Id);
        }

        trips.Add(trip.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return trip.Value.ToDto(user.UserId, group);
    }
}

/// <summary>Shares the trip with one of the owner's groups, or makes it private again (<c>GroupId = null</c>).</summary>
public sealed record ShareTripCommand(Guid TripId, Guid? GroupId) : ICommand<TripDto>;

internal sealed class ShareTripHandler(ITripRepository trips, IGroupDirectory directory, IUnitOfWork unitOfWork, ITripChangeNotifier notifier, ICurrentUser user)
    : ICommandHandler<ShareTripCommand, TripDto>
{
    public async Task<Result<TripDto>> Handle(ShareTripCommand command, CancellationToken cancellationToken)
    {
        if (await trips.LoadVisibleAsync(directory, command.TripId, user.UserId, cancellationToken) is not (Trip trip, _))
        {
            return TripErrors.NotFound;
        }

        if (trip.OwnerId != user.UserId)
        {
            return TripErrors.NotOwner;
        }

        GroupInfo? group = null;
        Result result;
        if (command.GroupId is { } groupId)
        {
            group = await directory.GetAsync(groupId, cancellationToken);
            if (group is null || !group.IsMember(user.UserId))
            {
                return TripErrors.GroupNotFound;
            }

            result = trip.LinkGroup(groupId);
        }
        else
        {
            result = trip.UnlinkGroup();
        }

        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(trip.Id, cancellationToken);
        return trip.ToDto(user.UserId, group);
    }
}

/// <summary>
/// Every trip edit has the same shape: load a trip the caller can see, check they may make this change,
/// apply one domain operation, save, tell other viewers, and return the updated trip.
/// </summary>
public abstract record TripChange(Guid TripId) : ICommand<TripDto>
{
    /// <summary>The plan is the owner's; the checklist is open to everyone the trip is shared with.</summary>
    internal virtual bool OwnerOnly => true;

    internal abstract Result Apply(Trip trip, string actorId);
}

public sealed record UpdateTripDetailsCommand(Guid TripId, string Name, string? Description) : TripChange(TripId)
{
    internal override Result Apply(Trip trip, string actorId) => trip.UpdateDetails(Name, Description);
}

public sealed record RescheduleTripCommand(Guid TripId, DateOnly StartDate, DateOnly EndDate) : TripChange(TripId)
{
    internal override Result Apply(Trip trip, string actorId) => trip.Reschedule(StartDate, EndDate);
}

public sealed record AddTrailToDayCommand(Guid TripId, int DayNumber, Guid TrailId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip, string actorId) => trip.AddTrail(DayNumber, TrailId);
}

public sealed record RemoveTrailFromDayCommand(Guid TripId, int DayNumber, Guid TrailId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip, string actorId) => trip.RemoveTrail(DayNumber, TrailId);
}

public sealed record UpdateTripDayCommand(Guid TripId, int DayNumber, string? Notes, string? Overnight) : TripChange(TripId)
{
    internal override Result Apply(Trip trip, string actorId) => trip.UpdateDay(DayNumber, Notes, Overnight);
}

public sealed record AddChecklistItemCommand(Guid TripId, string Text, ChecklistItemKind Kind = ChecklistItemKind.Personal) : TripChange(TripId)
{
    internal override bool OwnerOnly => false;

    internal override Result Apply(Trip trip, string actorId) =>
        trip.AddChecklistItem(actorId, Text, Kind) is { IsFailure: true } failed ? failed.Error! : Result.Success();
}

public sealed record SetChecklistItemDoneCommand(Guid TripId, Guid ItemId, bool IsDone) : TripChange(TripId)
{
    internal override bool OwnerOnly => false;

    internal override Result Apply(Trip trip, string actorId) => trip.SetChecklistItemDone(actorId, ItemId, IsDone);
}

public sealed record ClaimChecklistItemCommand(Guid TripId, Guid ItemId, bool Claim) : TripChange(TripId)
{
    internal override bool OwnerOnly => false;

    internal override Result Apply(Trip trip, string actorId) => trip.ClaimChecklistItem(actorId, ItemId, Claim);
}

public sealed record RemoveChecklistItemCommand(Guid TripId, Guid ItemId) : TripChange(TripId)
{
    internal override bool OwnerOnly => false;

    internal override Result Apply(Trip trip, string actorId) => trip.RemoveChecklistItem(actorId, ItemId);
}

public sealed record CompleteTripCommand(Guid TripId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip, string actorId) => trip.Complete();
}

public sealed record CancelTripCommand(Guid TripId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip, string actorId) => trip.Cancel();
}

internal sealed class TripChangeHandler<TCommand>(ITripRepository trips, IGroupDirectory directory, IUnitOfWork unitOfWork, ITripChangeNotifier notifier, ICurrentUser user)
    : ICommandHandler<TCommand, TripDto>
    where TCommand : TripChange
{
    public async Task<Result<TripDto>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        if (await trips.LoadVisibleAsync(directory, command.TripId, user.UserId, cancellationToken) is not (Trip trip, var group))
        {
            return TripErrors.NotFound;
        }

        if (command.OwnerOnly && trip.OwnerId != user.UserId)
        {
            return TripErrors.NotOwner;
        }

        var result = command.Apply(trip, user.UserId);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(trip.Id, cancellationToken);
        return trip.ToDto(user.UserId, group);
    }
}

public sealed record DeleteTripCommand(Guid TripId) : ICommand;

internal sealed class DeleteTripHandler(ITripRepository trips, IGroupDirectory directory, IUnitOfWork unitOfWork, ITripChangeNotifier notifier, ICurrentUser user)
    : ICommandHandler<DeleteTripCommand>
{
    public async Task<Result> Handle(DeleteTripCommand command, CancellationToken cancellationToken)
    {
        if (await trips.LoadVisibleAsync(directory, command.TripId, user.UserId, cancellationToken) is not (Trip trip, _))
        {
            return TripErrors.NotFound;
        }

        if (trip.OwnerId != user.UserId)
        {
            return TripErrors.NotOwner;
        }

        trips.Remove(trip);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.TripChangedAsync(trip.Id, cancellationToken);
        return Result.Success();
    }
}

public sealed record GetTripQuery(Guid TripId) : IQuery<TripDto>;

internal sealed class GetTripHandler(ITripRepository trips, IGroupDirectory directory, ICurrentUser user) : IQueryHandler<GetTripQuery, TripDto>
{
    public async Task<Result<TripDto>> Handle(GetTripQuery query, CancellationToken cancellationToken) =>
        await trips.LoadVisibleAsync(directory, query.TripId, user.UserId, cancellationToken) is (Trip trip, var group)
            ? trip.ToDto(user.UserId, group)
            : TripErrors.NotFound;
}

/// <summary>The caller's own trips and those shared with their groups; with <c>GroupId</c>, only that group's trips.</summary>
public sealed record GetMyTripsQuery(Guid? GroupId = null) : IQuery<IReadOnlyList<TripSummaryDto>>;

internal sealed class GetMyTripsHandler(ITripRepository trips, IGroupDirectory directory, ICurrentUser user) : IQueryHandler<GetMyTripsQuery, IReadOnlyList<TripSummaryDto>>
{
    public async Task<Result<IReadOnlyList<TripSummaryDto>>> Handle(GetMyTripsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Trip> visible;
        if (query.GroupId is { } groupId)
        {
            var group = await directory.GetAsync(groupId, cancellationToken);
            if (group is null || !group.IsMember(user.UserId))
            {
                return TripErrors.GroupNotFound;
            }

            visible = await trips.GetByGroupAsync(groupId, cancellationToken);
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
            .Select(t => t.ToSummary(user.UserId, t.GroupId is { } id ? groups[id] : null))
            .ToList();
    }
}
