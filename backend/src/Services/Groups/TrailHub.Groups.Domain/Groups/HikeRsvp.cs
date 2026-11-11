namespace TrailHub.Groups.Domain.Groups;

public sealed record HikeRsvp(string UserId, RsvpStatus Status, DateTime RespondedAtUtc);
