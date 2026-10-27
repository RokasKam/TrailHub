using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Trails.Domain.Trails;

public enum PointOfInterestType
{
    WaterSource,
    Shelter,
    Campsite,
    Viewpoint,
    Other,
}

public sealed class PointOfInterest : Entity<Guid>
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;

    private PointOfInterest()
    {
    }

    public string Name { get; private set; } = null!;

    public PointOfInterestType Type { get; private set; }

    public GeoPoint Location { get; private set; } = null!;

    public string? Description { get; private set; }

    internal static Result<PointOfInterest> Create(string name, PointOfInterestType type, GeoPoint location, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            return TrailErrors.InvalidPointOfInterestName;
        }

        if (description?.Length > MaxDescriptionLength)
        {
            return TrailErrors.DescriptionTooLong;
        }

        return new PointOfInterest
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Type = type,
            Location = location,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        };
    }

    /// <summary>Best-effort mapping from free-text GPX waypoint names/types (e.g. "Drinking Water", "Campground").</summary>
    public static PointOfInterestType InferType(string? text)
    {
        var value = text?.ToLowerInvariant() ?? string.Empty;
        return value switch
        {
            _ when value.Contains("water") || value.Contains("spring") || value.Contains("fountain") => PointOfInterestType.WaterSource,
            _ when value.Contains("shelter") || value.Contains("hut") || value.Contains("refuge") || value.Contains("lodge") => PointOfInterestType.Shelter,
            _ when value.Contains("camp") || value.Contains("tent") => PointOfInterestType.Campsite,
            _ when value.Contains("view") || value.Contains("scenic") || value.Contains("summit") || value.Contains("peak") => PointOfInterestType.Viewpoint,
            _ => PointOfInterestType.Other,
        };
    }
}

/// <param name="Url">Either an external https URL or a key in TrailHub's photo storage (resolved to a URL when read).</param>
public sealed record TrailPhoto(string Url, string? Caption, string UploadedBy, DateTime UploadedAtUtc);
