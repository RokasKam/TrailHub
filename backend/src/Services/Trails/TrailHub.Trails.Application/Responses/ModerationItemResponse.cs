using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Responses;

/// <param name="HiddenBy">Null while the trail is still in Discover.</param>
public sealed record ModerationItemResponse(Guid TrailId, string Name, string Region, string CreatedBy, HiddenBy? HiddenBy, int OpenReports, IReadOnlyList<ModerationReportResponse> Reports);
