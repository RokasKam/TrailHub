namespace TrailHub.Routes.Domain.Routes;

/// <summary>One day: track points <see cref="FromIndex"/> to <see cref="ToIndex"/>, ending at <see cref="Overnight"/> (null on the last day).</summary>
public sealed record PlannedStage(int FromIndex, int ToIndex, OvernightCandidate? Overnight);
