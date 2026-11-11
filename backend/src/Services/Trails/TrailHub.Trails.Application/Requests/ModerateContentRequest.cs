using TrailHub.Trails.Domain.Community;

namespace TrailHub.Trails.Application.Requests;

/// <param name="Action">Restore, Hide or Delete.</param>
public sealed record ModerateContentRequest(ModerationAction Action);
