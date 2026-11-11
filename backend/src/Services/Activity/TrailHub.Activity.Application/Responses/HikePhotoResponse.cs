namespace TrailHub.Activity.Application.Responses;

public sealed record HikePhotoResponse(Guid Id, string Url, string? Caption, DateTime AddedAtUtc);
