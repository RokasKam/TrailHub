using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;

namespace TrailHub.Activity.Application.Interfaces.Service;

/// <summary>Reports on hike comments and photos, and the moderators' queue.</summary>
public interface IContentModerationService
{
    /// <summary>Files (or updates) the caller's report on a comment. The third distinct open report hides it.</summary>
    Task ReportCommentAsync(Guid id, Guid commentId, ReportContentRequest request, CancellationToken cancellationToken);

    /// <summary>Files (or updates) the caller's report on a hike photo. The third distinct open report hides it.</summary>
    Task ReportPhotoAsync(Guid id, Guid photoId, ReportContentRequest request, CancellationToken cancellationToken);

    /// <summary>Moderators: comments and hike photos with open reports, and hidden ones, most reported first.</summary>
    Task<IReadOnlyList<ContentModerationItemResponse>> GetQueueAsync(CancellationToken cancellationToken);

    /// <summary>Moderators: restore, hide or delete a comment. Its open reports are resolved (deleted with it).</summary>
    Task ModerateCommentAsync(Guid commentId, ModerateContentRequest request, CancellationToken cancellationToken);

    /// <summary>Moderators: restore, hide or delete a hike photo. Its open reports are resolved (deleted with it).</summary>
    Task ModeratePhotoAsync(Guid photoId, ModerateContentRequest request, CancellationToken cancellationToken);
}
