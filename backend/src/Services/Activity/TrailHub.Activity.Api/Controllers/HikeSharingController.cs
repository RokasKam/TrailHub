using Microsoft.AspNetCore.Mvc;
using TrailHub.Activity.Api.Requests;
using TrailHub.Activity.Application.Interfaces.Service;
using TrailHub.Activity.Application.Requests;
using TrailHub.Activity.Application.Responses;
using TrailHub.BuildingBlocks.Web;

namespace TrailHub.Activity.Api.Controllers;

/// <summary>Sharing the caller's hikes with their groups, and the hikes' photos. Author only.</summary>
[Route("api/activities")]
public sealed class HikeSharingController(IHikeSharingService sharingService, IHikePhotoService photoService) : ApiController
{
    /// <summary>The groups the caller can share a hike with (the groups they belong to).</summary>
    [HttpGet("share-targets")]
    [ProducesResponseType<IReadOnlyList<ShareTargetResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ShareTargetResponse>>> ShareTargets(CancellationToken ct) =>
        Ok(await sharingService.GetShareTargetsAsync(ct));

    /// <summary>Shares a hike with some of the caller's groups, or updates its groups, caption and privacy.</summary>
    /// <remarks>Re-sharing keeps the original <c>sharedAtUtc</c>.</remarks>
    [HttpPut("{id:guid}/sharing")]
    [ProducesResponseType<HikeSharingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HikeSharingResponse>> Share(Guid id, ShareHikeRequest body, CancellationToken ct) =>
        Ok(await sharingService.ShareAsync(id, body, ct));

    /// <summary>Stops sharing a hike. Its likes and comments are kept and reappear if it's shared again.</summary>
    [HttpDelete("{id:guid}/sharing")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unshare(Guid id, CancellationToken ct)
    {
        await sharingService.UnshareAsync(id, ct);
        return NoContent();
    }

    /// <summary>Adds a photo to a hike (JPEG, PNG, HEIC or WebP, up to 10 MB; at most 6 per hike).</summary>
    [HttpPost("{id:guid}/photos")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType<HikePhotoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HikePhotoResponse>> UploadPhoto(Guid id, [FromForm] UploadHikePhotoForm form, CancellationToken ct)
    {
        await using var stream = form.File.OpenReadStream();
        var photo = await photoService.UploadAsync(id, stream, form.File.ContentType, form.File.Length, form.Caption, ct);
        return Created($"/api/activities/{id}", photo);
    }

    /// <summary>Removes a photo from a hike and deletes the stored file. Allowed for the hike's author and moderators.</summary>
    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        await photoService.DeleteAsync(id, photoId, ct);
        return NoContent();
    }
}
