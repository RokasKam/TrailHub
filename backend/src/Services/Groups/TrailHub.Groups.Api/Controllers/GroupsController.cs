using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Groups.Application.Groups;

namespace TrailHub.Groups.Api.Controllers;

/// <summary>Hiking groups, invite codes, scheduled group hikes and live locations.</summary>
/// <remarks>
/// Live location updates go over SignalR at <c>/hubs/groups</c> (methods <c>JoinGroup</c>,
/// <c>UpdateLocation</c>, <c>StopSharing</c>; events <c>LocationUpdated</c>, <c>LocationRemoved</c>).
/// <c>GET {id}/locations</c> is the REST snapshot of the same data.
/// </remarks>
[Route("api/groups")]
public sealed class GroupsController : ApiController
{
    /// <summary>Groups the caller belongs to, with their next scheduled hike.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GroupSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GroupSummaryDto>>> List([FromServices] IQueryHandler<GetMyGroupsQuery, IReadOnlyList<GroupSummaryDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetMyGroupsQuery(), ct));

    /// <summary>Members and upcoming hikes. The invite code is only included for the owner.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroupDto>> Get(Guid id, [FromServices] IQueryHandler<GetGroupQuery, GroupDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetGroupQuery(id), ct));

    /// <summary>Creates a group; the caller becomes its owner.</summary>
    [HttpPost]
    [ProducesResponseType<GroupDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GroupDto>> Create(CreateGroupCommand body, [FromServices] ICommandHandler<CreateGroupCommand, GroupDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(body, ct), g => CreatedAtAction(nameof(Get), new { id = g.Id }, g));

    /// <summary>Joins a group with its 8-character invite code (case-insensitive).</summary>
    [HttpPost("join")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupDto>> Join(JoinGroupCommand body, [FromServices] ICommandHandler<JoinGroupCommand, GroupDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(body, ct));

    /// <summary>Deletes the group (owner only).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] ICommandHandler<DeleteGroupCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new DeleteGroupCommand(id), ct));

    /// <summary>Leaves the group. The owner must transfer ownership first.</summary>
    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Leave(Guid id, [FromServices] ICommandHandler<LeaveGroupCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new LeaveGroupCommand(id), ct));

    /// <summary>Removes a member (owner only).</summary>
    [HttpDelete("{id:guid}/members/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, [FromServices] ICommandHandler<RemoveMemberCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new RemoveMemberCommand(id, userId), ct));

    /// <summary>Hands ownership to another member (owner only).</summary>
    [HttpPost("{id:guid}/owner")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GroupDto>> TransferOwnership(Guid id, TransferOwnershipRequest body, [FromServices] ICommandHandler<TransferOwnershipCommand, GroupDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new TransferOwnershipCommand(id, body.UserId), ct));

    /// <summary>Issues a new invite code; the old one stops working (owner only).</summary>
    [HttpPost("{id:guid}/invite-code")]
    [ProducesResponseType<GroupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GroupDto>> RegenerateInviteCode(Guid id, [FromServices] ICommandHandler<RegenerateInviteCodeCommand, GroupDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new RegenerateInviteCodeCommand(id), ct));

    /// <summary>Schedules a group hike (any member).</summary>
    [HttpPost("{id:guid}/hikes")]
    [ProducesResponseType<GroupHikeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GroupHikeDto>> ScheduleHike(Guid id, ScheduleHikeRequest b, [FromServices] ICommandHandler<ScheduleGroupHikeCommand, GroupHikeDto> handler, CancellationToken ct) =>
        FromResult(
            await handler.Handle(new ScheduleGroupHikeCommand(id, b.Title, b.TrailId, b.ScheduledAtUtc, b.MeetingLatitude, b.MeetingLongitude, b.Notes), ct),
            hike => CreatedAtAction(nameof(Get), new { id }, hike));

    /// <summary>Cancels a group hike (its organizer or the owner).</summary>
    [HttpDelete("{id:guid}/hikes/{hikeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelHike(Guid id, Guid hikeId, [FromServices] ICommandHandler<CancelGroupHikeCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new CancelGroupHikeCommand(id, hikeId), ct));

    /// <summary>Answers a scheduled hike: Going, Maybe or NotGoing (replaces any previous answer).</summary>
    [HttpPut("{id:guid}/hikes/{hikeId:guid}/rsvp")]
    [ProducesResponseType<GroupHikeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroupHikeDto>> Rsvp(Guid id, Guid hikeId, RsvpRequest body, [FromServices] ICommandHandler<RsvpGroupHikeCommand, GroupHikeDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new RsvpGroupHikeCommand(id, hikeId, body.Status), ct));

    /// <summary>
    /// Shares the caller's position with the group. Used by background location updates, which can't
    /// keep a WebSocket open; members viewing the live map receive it over SignalR either way.
    /// </summary>
    [HttpPost("{id:guid}/locations/mine")]
    [ProducesResponseType<LiveLocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LiveLocationDto>> ShareLocation(Guid id, ShareLocationRequest body, [FromServices] ICommandHandler<ShareLocationCommand, LiveLocationDto> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new ShareLocationCommand(id, body.Latitude, body.Longitude, body.Elevation, body.AccuracyMeters, body.HeadingDegrees), ct));

    /// <summary>Stops sharing: removes the caller from the group's live map.</summary>
    [HttpDelete("{id:guid}/locations/mine")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> StopSharing(Guid id, [FromServices] ICommandHandler<StopSharingLocationCommand> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new StopSharingLocationCommand(id), ct));

    /// <summary>Latest shared position of each member (entries expire after 15 minutes).</summary>
    [HttpGet("{id:guid}/locations")]
    [ProducesResponseType<IReadOnlyList<LiveLocationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LiveLocationDto>>> Locations(Guid id, [FromServices] IQueryHandler<GetLiveLocationsQuery, IReadOnlyList<LiveLocationDto>> handler, CancellationToken ct) =>
        FromResult(await handler.Handle(new GetLiveLocationsQuery(id), ct));
}

public sealed record TransferOwnershipRequest(string UserId);

public sealed record RsvpRequest(Domain.Groups.RsvpStatus Status);

public sealed record ShareLocationRequest(double Latitude, double Longitude, double? Elevation, double? AccuracyMeters, double? HeadingDegrees);

public sealed record ScheduleHikeRequest(string Title, Guid? TrailId, DateTime ScheduledAtUtc, double? MeetingLatitude, double? MeetingLongitude, string? Notes);
