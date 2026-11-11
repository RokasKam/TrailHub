using TrailHub.Groups.Application.Responses;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Mappings;

internal static class GroupMappings
{
    public static GroupResponse ToResponse(this HikingGroup g, string viewerId, DateTime nowUtc) => new(
        g.Id,
        g.Name,
        g.Description,
        g.OwnerId == viewerId ? g.InviteCode : null,
        g.Members.Select(m => new GroupMemberResponse(m.UserId, m.DisplayName, m.Role, m.JoinedAtUtc)).ToList(),
        g.Hikes
            .Where(h => h.ScheduledAtUtc > nowUtc && h.Status == GroupHikeStatus.Scheduled)
            .OrderBy(h => h.ScheduledAtUtc)
            .Select(h => h.ToResponse())
            .ToList(),
        g.CreatedAtUtc);

    public static GroupHikeResponse ToResponse(this GroupHike h) =>
        new(
            h.Id,
            h.Title,
            h.TrailId,
            h.ScheduledAtUtc,
            h.MeetingPoint?.Latitude,
            h.MeetingPoint?.Longitude,
            h.Notes,
            h.CreatedBy,
            h.Status,
            h.Rsvps.Select(r => new RsvpResponse(r.UserId, r.Status, r.RespondedAtUtc)).ToList());
}
