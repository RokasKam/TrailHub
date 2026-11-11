using TrailHub.Trips.Domain.Gear;

namespace TrailHub.Trips.Application.Responses;

/// <param name="Id">A GUID for your own lists; <c>starter:…</c> for the built-in ones.</param>
/// <param name="IsStarter">Built-in and read-only; save a copy to change it.</param>
public sealed record GearListResponse(string Id, string Name, IReadOnlyList<GearItem> Items, bool IsStarter, DateTime? UpdatedAtUtc);
