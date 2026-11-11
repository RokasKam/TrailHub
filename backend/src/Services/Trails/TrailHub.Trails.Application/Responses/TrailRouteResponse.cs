namespace TrailHub.Trails.Application.Responses;

public sealed record TrailRouteResponse(Guid TrailId, int TotalPoints, IReadOnlyList<RoutePointResponse> Points);
