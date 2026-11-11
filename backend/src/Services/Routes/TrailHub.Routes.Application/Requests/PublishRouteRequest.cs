using TrailHub.Routes.Domain.Routes;

namespace TrailHub.Routes.Application.Requests;

public sealed record PublishRouteRequest(string Name, string? Description, string Region, PlanDifficulty Difficulty);
