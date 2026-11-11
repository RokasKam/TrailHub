using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Requests;

public sealed record ChecklistItemsRequest(IReadOnlyList<GearItem> Items);
