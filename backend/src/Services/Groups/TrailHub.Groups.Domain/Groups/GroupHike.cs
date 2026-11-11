using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Groups.Domain.Groups;

public sealed class GroupHike : Entity<Guid>
{
    private readonly List<HikeRsvp> _rsvps = [];

    private GroupHike()
    {
    }

    internal GroupHike(string title, Guid? trailId, DateTime scheduledAtUtc, GeoPoint? meetingPoint, string? notes, string createdBy)
    {
        Id = Guid.CreateVersion7();
        Title = title;
        TrailId = trailId;
        ScheduledAtUtc = scheduledAtUtc;
        MeetingPoint = meetingPoint;
        Notes = notes;
        CreatedBy = createdBy;
        Status = GroupHikeStatus.Scheduled;
    }

    public string Title { get; private set; } = null!;

    public Guid? TrailId { get; private set; }

    public DateTime ScheduledAtUtc { get; private set; }

    public GeoPoint? MeetingPoint { get; private set; }

    public string? Notes { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public GroupHikeStatus Status { get; internal set; }

    public IReadOnlyList<HikeRsvp> Rsvps => _rsvps;

    internal void SetRsvp(string userId, RsvpStatus status, DateTime nowUtc)
    {
        _rsvps.RemoveAll(r => r.UserId == userId);
        _rsvps.Add(new HikeRsvp(userId, status, nowUtc));
    }

    internal void RemoveRsvp(string userId) => _rsvps.RemoveAll(r => r.UserId == userId);
}
