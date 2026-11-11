using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Requests;

public sealed record UpdateTrailRequest(string Name, string? Description, string Region, Difficulty Difficulty);
