using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Responses;

/// <summary>A trail photo with open reports, or hidden, in the moderators' queue.</summary>
/// <param name="HiddenBy">Null while the photo is still shown.</param>
public sealed record PhotoModerationItemResponse(
    Guid TrailId,
    string TrailName,
    Guid PhotoId,
    string Url,
    string? Caption,
    string UploadedBy,
    HiddenBy? HiddenBy,
    int OpenReports,
    IReadOnlyList<PhotoReportResponse> Reports);
