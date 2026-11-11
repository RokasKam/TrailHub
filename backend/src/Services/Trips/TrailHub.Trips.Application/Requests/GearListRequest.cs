using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Requests;

public sealed record GearListRequest(string Name, IReadOnlyList<GearItem> Items);
