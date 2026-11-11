namespace TrailHub.Trails.Application.Responses;

public sealed record TrailPhotoResponse(string Url, string? Caption, DateTime UploadedAtUtc);
