using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Groups.Application.Dtos;
using TrailHub.Groups.Application.Interfaces.Service;
using TrailHub.Groups.Application.Requests;
using TrailHub.Groups.Application.Responses;

namespace TrailHub.Groups.Api.Controllers;

/// <summary>Hiking groups, invite codes, scheduled group hikes and live locations.</summary>
/// <remarks>
/// Live location updates go over SignalR at <c>/hubs/groups</c> (methods <c>JoinGroup</c>,
/// <c>UpdateLocation</c>, <c>StopSharing</c>; events <c>LocationUpdated</c>, <c>LocationRemoved</c>).
/// <c>GET {id}/locations</c> is the REST snapshot of the same data.
/// </remarks>
[Route("api/groups")]
public sealed class GroupsController(
    IGroupService groupService,
    IGroupHikeService groupHikeService,
    ILiveLocationService liveLocationService) : ApiController
{
    /// <summary>Groups the caller belongs to, with their next scheduled hike.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GroupSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GroupSummaryResponse>>> List(CancellationToken ct) =>
        Ok(await groupService.GetMyGroupsAsync(ct));

    /// <summary>Members and upcoming hikes. The invite code is only included for the owner.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GroupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await groupService.GetGroupAsync(id, ct));

    /// <summary>Creates a group; the caller becomes its owner.</summary>
    [HttpPost]
    [ProducesResponseType<GroupResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GroupResponse>> Create(CreateGroupRequest body, CancellationToken ct)
    {
        var group = await groupService.CreateAsync(body, ct);
        return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
    }

    /// <summary>Joins a group with its 8-character invite code (case-insensitive).</summary>
    [HttpPost("join")]
    [ProducesResponseType<GroupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupResponse>> Join(JoinGroupRequest body, CancellationToken ct) =>
        Ok(await groupService.JoinAsync(body, ct));

    /// <summary>Deletes the group (owner only).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await groupService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Leaves the group. The owner must transfer ownership first.</summary>
    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await groupService.LeaveAsync(id, ct);
        return NoContent();
    }

    /// <summary>Removes a member (owner only).</summary>
    [HttpDelete("{id:guid}/members/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, CancellationToken ct)
    {
        await groupService.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }

    /// <summary>Hands ownership to another member (owner only).</summary>
    [HttpPost("{id:guid}/owner")]
    [ProducesResponseType<GroupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GroupResponse>> TransferOwnership(Guid id, TransferOwnershipRequest body, CancellationToken ct) =>
        Ok(await groupService.TransferOwnershipAsync(id, body, ct));

    /// <summary>Issues a new invite code; the old one stops working (owner only).</summary>
    [HttpPost("{id:guid}/invite-code")]
    [ProducesResponseType<GroupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GroupResponse>> RegenerateInviteCode(Guid id, CancellationToken ct) =>
        Ok(await groupService.RegenerateInviteCodeAsync(id, ct));

    /// <summary>Schedules a group hike (any member).</summary>
    [HttpPost("{id:guid}/hikes")]
    [ProducesResponseType<GroupHikeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GroupHikeResponse>> ScheduleHike(Guid id, ScheduleHikeRequest body, CancellationToken ct)
    {
        var hike = await groupHikeService.ScheduleAsync(id, body, ct);
        return CreatedAtAction(nameof(Get), new { id }, hike);
    }

    /// <summary>Cancels a group hike (its organizer or the owner).</summary>
    [HttpDelete("{id:guid}/hikes/{hikeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelHike(Guid id, Guid hikeId, CancellationToken ct)
    {
        await groupHikeService.CancelAsync(id, hikeId, ct);
        return NoContent();
    }

    /// <summary>Answers a scheduled hike: Going, Maybe or NotGoing (replaces any previous answer).</summary>
    [HttpPut("{id:guid}/hikes/{hikeId:guid}/rsvp")]
    [ProducesResponseType<GroupHikeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupHikeResponse>> Rsvp(Guid id, Guid hikeId, RsvpRequest body, CancellationToken ct) =>
        Ok(await groupHikeService.RsvpAsync(id, hikeId, body, ct));

    /// <summary>
    /// Shares the caller's position with the group. Used by background location updates, which can't
    /// keep a WebSocket open; members viewing the live map receive it over SignalR either way.
    /// </summary>
    [HttpPost("{id:guid}/locations/mine")]
    [ProducesResponseType<LiveLocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LiveLocationDto>> ShareLocation(Guid id, ShareLocationRequest body, CancellationToken ct) =>
        Ok(await liveLocationService.ShareAsync(id, body, ct));

    /// <summary>Stops sharing: removes the caller from the group's live map.</summary>
    [HttpDelete("{id:guid}/locations/mine")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StopSharing(Guid id, CancellationToken ct)
    {
        await liveLocationService.StopSharingAsync(id, ct);
        return NoContent();
    }

    /// <summary>Latest shared position of each member (entries expire after 15 minutes).</summary>
    [HttpGet("{id:guid}/locations")]
    [ProducesResponseType<IReadOnlyList<LiveLocationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LiveLocationDto>>> Locations(Guid id, CancellationToken ct) =>
        Ok(await liveLocationService.GetLocationsAsync(id, ct));
}
