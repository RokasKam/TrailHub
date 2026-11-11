using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Requests;

/// <param name="Action">Restore, Hide or Delete.</param>
public sealed record ModerateContentRequest(ModerationAction Action);
