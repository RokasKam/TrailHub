using TrailHub.Trails.Application.Requests;
using TrailHub.Trails.Application.Responses;

namespace TrailHub.Trails.Application.Interfaces.Service;

/// <summary>Trail photo reports and the moderators' photo queue.</summary>
public interface IPhotoModerationService
{
    /// <summary>Files (or updates) the caller's report. The third distinct open report hides the photo.</summary>
    Task ReportAsync(Guid trailId, Guid photoId, ReportContentRequest request, CancellationToken cancellationToken);

    /// <summary>Moderators: photos with open reports, and hidden photos, most reported first.</summary>
    Task<IReadOnlyList<PhotoModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken);

    /// <summary>Moderators: restore, hide or delete a photo. Its open reports are resolved (deleted with the photo).</summary>
    Task ModerateAsync(Guid trailId, Guid photoId, ModerateContentRequest request, CancellationToken cancellationToken);
}
