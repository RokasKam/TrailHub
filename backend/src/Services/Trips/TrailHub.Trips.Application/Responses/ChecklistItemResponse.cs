using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Responses;

/// <param name="IsDone">For personal items, whether the viewer has packed theirs; for shared items, whether the person bringing it has.</param>
public sealed record ChecklistItemResponse(
    Guid Id,
    string Text,
    ChecklistItemKind Kind,
    bool IsDone,
    string? ClaimedBy,
    string? ClaimedByName,
    string AddedBy,
    bool CanRemove);
