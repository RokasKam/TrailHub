using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Domain.Gear;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Trips;

public interface IGearListRepository
{
    Task<GearList?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<GearList>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    Task<int> CountByOwnerAsync(string ownerId, CancellationToken cancellationToken);

    void Add(GearList list);

    void Remove(GearList list);
}

/// <param name="Id">A GUID for your own lists; <c>starter:…</c> for the built-in ones.</param>
/// <param name="IsStarter">Built-in and read-only; save a copy to change it.</param>
public sealed record GearListDto(string Id, string Name, IReadOnlyList<GearItem> Items, bool IsStarter, DateTime? UpdatedAtUtc);

internal static class GearMappings
{
    public const string StarterPrefix = "starter:";

    public static GearListDto ToDto(this GearList l) => new(l.Id.ToString(), l.Name, l.Items, false, l.UpdatedAtUtc);

    public static GearListDto ToDto(this StarterLists.Starter s) => new(StarterPrefix + s.Id, s.Name, s.Items, true, null);
}

public sealed record GetGearListsQuery : IQuery<IReadOnlyList<GearListDto>>;

/// <summary>Your lists, most recently changed first, then the starter lists.</summary>
internal sealed class GetGearListsHandler(IGearListRepository lists, ICurrentUser user) : IQueryHandler<GetGearListsQuery, IReadOnlyList<GearListDto>>
{
    public async Task<Result<IReadOnlyList<GearListDto>>> Handle(GetGearListsQuery query, CancellationToken cancellationToken)
    {
        var mine = await lists.GetByOwnerAsync(user.UserId, cancellationToken);
        return mine.Select(l => l.ToDto()).Concat(StarterLists.All.Select(s => s.ToDto())).ToList();
    }
}

public sealed record SaveGearListCommand(Guid? ListId, string Name, IReadOnlyList<GearItem> Items) : ICommand<GearListDto>;

/// <summary>Creates a list (<c>ListId = null</c>) or replaces one of yours.</summary>
internal sealed class SaveGearListHandler(IGearListRepository lists, IUnitOfWork unitOfWork, ICurrentUser user, TimeProvider clock)
    : ICommandHandler<SaveGearListCommand, GearListDto>
{
    public async Task<Result<GearListDto>> Handle(SaveGearListCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        GearList list;
        if (command.ListId is { } id)
        {
            if (await lists.GetByIdAsync(id, cancellationToken) is not { } existing || existing.OwnerId != user.UserId)
            {
                return GearErrors.NotFound;
            }

            if (existing.Update(command.Name, command.Items, now) is { IsFailure: true } failed)
            {
                return failed.Error!;
            }

            list = existing;
        }
        else
        {
            if (await lists.CountByOwnerAsync(user.UserId, cancellationToken) >= GearList.MaxListsPerUser)
            {
                return GearErrors.TooManyLists;
            }

            var created = GearList.Create(user.UserId, command.Name, command.Items, now);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            lists.Add(created.Value);
            list = created.Value;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return list.ToDto();
    }
}

public sealed record DeleteGearListCommand(Guid ListId) : ICommand;

internal sealed class DeleteGearListHandler(IGearListRepository lists, IUnitOfWork unitOfWork, ICurrentUser user) : ICommandHandler<DeleteGearListCommand>
{
    public async Task<Result> Handle(DeleteGearListCommand command, CancellationToken cancellationToken)
    {
        if (await lists.GetByIdAsync(command.ListId, cancellationToken) is not { } list || list.OwnerId != user.UserId)
        {
            return GearErrors.NotFound;
        }

        lists.Remove(list);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Adds several items to a trip's checklist; duplicates are skipped. Anyone who can use the checklist may do it.</summary>
public sealed record AddChecklistItemsCommand(Guid TripId, IReadOnlyList<GearItem> Items) : TripChange(TripId)
{
    internal override bool OwnerOnly => false;

    internal override Result Apply(Trip trip, string actorId)
    {
        trip.AddChecklistItems(actorId, Items.Select(i => (i.Text, i.Kind)));
        return Result.Success();
    }
}

/// <summary>Copies a gear list (yours or a starter list) into a trip's checklist.</summary>
public sealed record AddGearListToTripCommand(Guid TripId, string ListId) : ICommand<TripDto>;

internal sealed class AddGearListToTripHandler(IGearListRepository lists, ICommandHandler<AddChecklistItemsCommand, TripDto> addItems, ICurrentUser user)
    : ICommandHandler<AddGearListToTripCommand, TripDto>
{
    public async Task<Result<TripDto>> Handle(AddGearListToTripCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<GearItem>? items = null;
        if (command.ListId.StartsWith(GearMappings.StarterPrefix, StringComparison.Ordinal))
        {
            items = StarterLists.Find(command.ListId[GearMappings.StarterPrefix.Length..])?.Items;
        }
        else if (Guid.TryParse(command.ListId, out var id) && await lists.GetByIdAsync(id, cancellationToken) is { } list && list.OwnerId == user.UserId)
        {
            items = list.Items;
        }

        return items is null ? GearErrors.NotFound : await addItems.Handle(new AddChecklistItemsCommand(command.TripId, items), cancellationToken);
    }
}

/// <summary>Saves a trip's checklist as one of your gear lists, keeping each item's personal/group kind.</summary>
public sealed record SaveChecklistAsGearListCommand(Guid TripId, string Name) : ICommand<GearListDto>;

internal sealed class SaveChecklistAsGearListHandler(ITripRepository trips, IGroupDirectory directory, ICommandHandler<SaveGearListCommand, GearListDto> save, ICurrentUser user)
    : ICommandHandler<SaveChecklistAsGearListCommand, GearListDto>
{
    public async Task<Result<GearListDto>> Handle(SaveChecklistAsGearListCommand command, CancellationToken cancellationToken)
    {
        if (await trips.LoadVisibleAsync(directory, command.TripId, user.UserId, cancellationToken) is not (Trip trip, _))
        {
            return TripErrors.NotFound;
        }

        return await save.Handle(new SaveGearListCommand(null, command.Name, trip.Checklist.Select(i => new GearItem(i.Text, i.Kind)).ToList()), cancellationToken);
    }
}

/// <param name="TentNights">From the trip's planned route, if the app has it; otherwise guessed from the days' overnight names.</param>
public sealed record GetGearSuggestionsQuery(
    Guid TripId,
    int? TentNights,
    int? HutNights,
    int? HotelNights,
    double? MaxElevationMeters,
    int? LongestDayMinutes) : IQuery<IReadOnlyList<GearSuggestion>>;

internal sealed class GetGearSuggestionsHandler(ITripRepository trips, IGroupDirectory directory, ICurrentUser user)
    : IQueryHandler<GetGearSuggestionsQuery, IReadOnlyList<GearSuggestion>>
{
    public async Task<Result<IReadOnlyList<GearSuggestion>>> Handle(GetGearSuggestionsQuery query, CancellationToken cancellationToken)
    {
        if (await trips.LoadVisibleAsync(directory, query.TripId, user.UserId, cancellationToken) is not (Trip trip, _))
        {
            return TripErrors.NotFound;
        }

        // Without route facts, read the kind of night from what the planner typed ("Refuge du Lac Blanc", "Camping …").
        var guessed = trip.Days.Where(d => d.DayNumber < trip.DayCount && d.Overnight is not null).Select(d => OvernightKind(d.Overnight!)).ToList();
        var facts = new TripGearFacts(
            trip.DayCount,
            query.TentNights ?? guessed.Count(k => k == "tent"),
            query.HutNights ?? guessed.Count(k => k == "hut"),
            query.HotelNights ?? guessed.Count(k => k == "hotel"),
            query.MaxElevationMeters,
            query.LongestDayMinutes,
            trip.GroupId is not null);
        return GearSuggestions.For(facts, trip.Checklist.Select(i => i.Text)).ToList();
    }

    internal static string? OvernightKind(string overnight)
    {
        var o = overnight.ToLowerInvariant();
        string[] huts = ["hut", "hütte", "huette", "refuge", "rifugio", "cabane", "chata", "schronisko", "bivouac", "bivacco", "shelter"];
        string[] tents = ["camp", "tent", "bivy"];
        string[] hotels = ["hotel", "hostel", "guest", "inn", "auberge", "gîte", "gite", "pension", "b&b", "chalet"];
        return huts.Any(o.Contains) ? "hut" : tents.Any(o.Contains) ? "tent" : hotels.Any(o.Contains) ? "hotel" : null;
    }
}
