namespace TrailHub.Activity.Api.Requests;

/// <summary>Multipart form for <c>POST /api/activities/import</c>.</summary>
public sealed record ImportActivityForm
{
    /// <summary>A .gpx file whose track points all have timestamps.</summary>
    public required IFormFile File { get; init; }

    /// <summary>Defaults to the name in the GPX file.</summary>
    public string? Name { get; init; }

    public Guid? TrailId { get; init; }
}
