namespace TrailHub.BuildingBlocks.Domain;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,

    /// <summary>A dependency (e.g. a third-party API) is down; worth retrying later.</summary>
    Unavailable,
}
