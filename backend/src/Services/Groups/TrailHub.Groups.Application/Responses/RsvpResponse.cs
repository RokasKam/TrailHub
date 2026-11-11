using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Application.Responses;

public sealed record RsvpResponse(string UserId, RsvpStatus Status, DateTime RespondedAtUtc);
