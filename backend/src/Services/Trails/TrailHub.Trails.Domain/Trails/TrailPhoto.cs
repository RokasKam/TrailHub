using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Trails.Domain.Trails;

/// <param name="Url">Either an external https URL or a key in TrailHub's photo storage (resolved to a URL when read).</param>
public sealed record TrailPhoto(string Url, string? Caption, string UploadedBy, DateTime UploadedAtUtc);
