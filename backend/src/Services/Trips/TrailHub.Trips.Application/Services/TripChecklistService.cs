using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.BuildingBlocks.Domain;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Application.Mappings;
using TrailHub.Trips.Application.Requests;
using TrailHub.Trips.Application.Responses;
using TrailHub.Trips.Domain.Gear;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Services;

internal sealed class TripChecklistService(
    TripEditor editor,
    IGearListRepository gearLists,
    IGearListService gearListService,
    ICurrentUser user) : ITripChecklistService
{
    public Task<TripResponse> AddItemAsync(Guid tripId, ChecklistItemRequest request, CancellationToken cancellationToken) =>
        editor.ChangeChecklistAsync(
            tripId,
            (trip, actorId) => trip.AddChecklistItem(actorId, request.Text, request.Kind ?? ChecklistItemKind.Personal) is { IsFailure: true } failed ? failed.Error! : Result.Success(),
            cancellationToken);

    public Task<TripResponse> AddItemsAsync(Guid tripId, ChecklistItemsRequest request, CancellationToken cancellationToken) =>
        AddItemsAsync(tripId, request.Items, cancellationToken);

    public Task<TripResponse> SetItemDoneAsync(Guid tripId, Guid itemId, ChecklistDoneRequest request, CancellationToken cancellationToken) =>
        editor.ChangeChecklistAsync(tripId, (trip, actorId) => trip.SetChecklistItemDone(actorId, itemId, request.IsDone), cancellationToken);

    public Task<TripResponse> ClaimItemAsync(Guid tripId, Guid itemId, ChecklistClaimRequest request, CancellationToken cancellationToken) =>
        editor.ChangeChecklistAsync(tripId, (trip, actorId) => trip.ClaimChecklistItem(actorId, itemId, request.Claim), cancellationToken);

    public Task<TripResponse> RemoveItemAsync(Guid tripId, Guid itemId, CancellationToken cancellationToken) =>
        editor.ChangeChecklistAsync(tripId, (trip, actorId) => trip.RemoveChecklistItem(actorId, itemId), cancellationToken);

    public async Task<TripResponse> AddGearListAsync(Guid tripId, string listId, CancellationToken cancellationToken)
    {
        IReadOnlyList<GearItem>? items = null;
        if (listId.StartsWith(GearListMappings.StarterPrefix, StringComparison.Ordinal))
        {
            items = StarterLists.Find(listId[GearListMappings.StarterPrefix.Length..])?.Items;
        }
        else if (Guid.TryParse(listId, out var id) && await gearLists.GetByIdAsync(id, cancellationToken) is { } list && list.OwnerId == user.UserId)
        {
            items = list.Items;
        }

        return items is null
            ? throw GearErrors.NotFound.ToException()
            : await AddItemsAsync(tripId, items, cancellationToken);
    }

    public async Task<GearListResponse> SaveAsGearListAsync(Guid tripId, SaveAsGearListRequest request, CancellationToken cancellationToken)
    {
        var (trip, _) = await editor.LoadVisibleAsync(tripId, cancellationToken);
        return await gearListService.CreateAsync(
            new GearListRequest(request.Name, trip.Checklist.Select(i => new GearItem(i.Text, i.Kind)).ToList()),
            cancellationToken);
    }

    private Task<TripResponse> AddItemsAsync(Guid tripId, IReadOnlyList<GearItem> items, CancellationToken cancellationToken) =>
        editor.ChangeChecklistAsync(
            tripId,
            (trip, actorId) =>
            {
                trip.AddChecklistItems(actorId, items.Select(i => (i.Text, i.Kind)));
                return Result.Success();
            },
            cancellationToken);
}
