using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Requests;

public sealed record ChecklistItemRequest(string Text, ChecklistItemKind? Kind);
