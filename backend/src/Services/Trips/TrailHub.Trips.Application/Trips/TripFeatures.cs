using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Trips;

public interface ITripRepository
{
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Trip>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    void Add(Trip trip);

    void Remove(Trip trip);
}

public sealed record TripDayDto(int DayNumber, DateOnly Date, string? Notes, string? Overnight, IReadOnlyList<Guid> TrailIds);

public sealed record ChecklistItemDto(Guid Id, string Text, bool IsDone);

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
    DateTime CreatedAtUtc);

public sealed record TripSummaryDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, int DayCount, TripStatus Status, int TrailCount);

internal static class TripMappings
{
    public static TripDto ToDto(this Trip t) => new(
        t.Id,
        t.Name,
        t.Description,
        t.StartDate,
        t.EndDate,
        t.DayCount,
        t.Status,
        t.Days.OrderBy(d => d.DayNumber).Select(d => new TripDayDto(d.DayNumber, t.StartDate.AddDays(d.DayNumber - 1), d.Notes, d.Overnight, d.TrailIds.ToList())).ToList(),
        t.Checklist.Select(i => new ChecklistItemDto(i.Id, i.Text, i.IsDone)).ToList(),
        t.CreatedAtUtc);
}

public sealed record CreateTripCommand(string Name, string? Description, DateOnly StartDate, DateOnly EndDate) : ICommand<TripDto>;

internal sealed class CreateTripHandler(ITripRepository trips, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<CreateTripCommand, TripDto>
{
    public async Task<Result<TripDto>> Handle(CreateTripCommand command, CancellationToken cancellationToken)
    {
        var trip = Trip.Create(user.UserId, command.Name, command.Description, command.StartDate, command.EndDate, clock.GetUtcNow().UtcDateTime);
        if (trip.IsFailure)
        {
            return trip.Error!;
        }

        trips.Add(trip.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return trip.Value.ToDto();
    }
}

/// <summary>
/// Every trip edit has the same shape: load the caller's trip, apply one domain operation, save,
/// return the updated trip. Other users' trips are reported as not found so ids can't be probed.
/// </summary>
public abstract record TripChange(Guid TripId) : ICommand<TripDto>
{
    internal abstract Result Apply(Trip trip);
}

public sealed record UpdateTripDetailsCommand(Guid TripId, string Name, string? Description) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.UpdateDetails(Name, Description);
}

public sealed record RescheduleTripCommand(Guid TripId, DateOnly StartDate, DateOnly EndDate) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.Reschedule(StartDate, EndDate);
}

public sealed record AddTrailToDayCommand(Guid TripId, int DayNumber, Guid TrailId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.AddTrail(DayNumber, TrailId);
}

public sealed record RemoveTrailFromDayCommand(Guid TripId, int DayNumber, Guid TrailId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.RemoveTrail(DayNumber, TrailId);
}

public sealed record UpdateTripDayCommand(Guid TripId, int DayNumber, string? Notes, string? Overnight) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.UpdateDay(DayNumber, Notes, Overnight);
}

public sealed record AddChecklistItemCommand(Guid TripId, string Text) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.AddChecklistItem(Text) is { IsFailure: true } failed ? failed.Error! : Result.Success();
}

public sealed record SetChecklistItemDoneCommand(Guid TripId, Guid ItemId, bool IsDone) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.SetChecklistItemDone(ItemId, IsDone);
}

public sealed record RemoveChecklistItemCommand(Guid TripId, Guid ItemId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.RemoveChecklistItem(ItemId);
}

public sealed record CompleteTripCommand(Guid TripId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.Complete();
}

public sealed record CancelTripCommand(Guid TripId) : TripChange(TripId)
{
    internal override Result Apply(Trip trip) => trip.Cancel();
}

internal sealed class TripChangeHandler<TCommand>(ITripRepository trips, IUnitOfWork unitOfWork, ICurrentUser user)
    : ICommandHandler<TCommand, TripDto>
    where TCommand : TripChange
{
    public async Task<Result<TripDto>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var trip = await trips.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null || trip.OwnerId != user.UserId)
        {
            return TripErrors.NotFound;
        }

        var result = command.Apply(trip);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return trip.ToDto();
    }
}

public sealed record DeleteTripCommand(Guid TripId) : ICommand;

internal sealed class DeleteTripHandler(ITripRepository trips, IUnitOfWork unitOfWork, ICurrentUser user) : ICommandHandler<DeleteTripCommand>
{
    public async Task<Result> Handle(DeleteTripCommand command, CancellationToken cancellationToken)
    {
        var trip = await trips.GetByIdAsync(command.TripId, cancellationToken);
        if (trip is null || trip.OwnerId != user.UserId)
        {
            return TripErrors.NotFound;
        }

        trips.Remove(trip);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed record GetTripQuery(Guid TripId) : IQuery<TripDto>;

internal sealed class GetTripHandler(ITripRepository trips, ICurrentUser user) : IQueryHandler<GetTripQuery, TripDto>
{
    public async Task<Result<TripDto>> Handle(GetTripQuery query, CancellationToken cancellationToken)
    {
        var trip = await trips.GetByIdAsync(query.TripId, cancellationToken);
        return trip is null || trip.OwnerId != user.UserId ? TripErrors.NotFound : trip.ToDto();
    }
}

public sealed record GetMyTripsQuery : IQuery<IReadOnlyList<TripSummaryDto>>;

internal sealed class GetMyTripsHandler(ITripRepository trips, ICurrentUser user) : IQueryHandler<GetMyTripsQuery, IReadOnlyList<TripSummaryDto>>
{
    public async Task<Result<IReadOnlyList<TripSummaryDto>>> Handle(GetMyTripsQuery query, CancellationToken cancellationToken)
    {
        var mine = await trips.GetByOwnerAsync(user.UserId, cancellationToken);
        return mine
            .Select(t => new TripSummaryDto(t.Id, t.Name, t.StartDate, t.EndDate, t.DayCount, t.Status, t.Days.Sum(d => d.TrailIds.Count)))
            .ToList();
    }
}
