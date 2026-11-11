namespace TrailHub.Routes.Domain.Routes;

public sealed record Itinerary(IReadOnlyList<PlannedStage> Stages, IReadOnlyList<string> Warnings);
