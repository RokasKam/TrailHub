using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trails.Domain.Trails;

public static class TrailErrors
{
    public static readonly Error NotFound = Error.NotFound("Trail.NotFound", "The trail does not exist.");
    public static readonly Error NotOwner = Error.Forbidden("Trail.NotOwner", "Only the user who created the trail can change it.");
    public static readonly Error InvalidName = Error.Validation("Trail.InvalidName", $"Name is required and must be at most {Trail.MaxNameLength} characters.");
    public static readonly Error InvalidRegion = Error.Validation("Trail.InvalidRegion", $"Region is required and must be at most {Trail.MaxRegionLength} characters.");
    public static readonly Error DescriptionTooLong = Error.Validation("Trail.DescriptionTooLong", "Description is too long.");
    public static readonly Error RouteTooShort = Error.Validation("Trail.RouteTooShort", "A route needs at least two points.");
    public static readonly Error RouteTooLong = Error.Validation("Trail.RouteTooLong", $"A route can have at most {Trail.MaxRoutePoints} points.");
    public static readonly Error TooManyPointsOfInterest = Error.Validation("Trail.TooManyPointsOfInterest", $"A trail can have at most {Trail.MaxPointsOfInterest} points of interest.");
    public static readonly Error PointOfInterestNotFound = Error.NotFound("Trail.PointOfInterestNotFound", "The point of interest does not exist.");
    public static readonly Error InvalidPointOfInterestName = Error.Validation("Trail.InvalidPointOfInterestName", "Point of interest name is required.");
    public static readonly Error TooManyPhotos = Error.Validation("Trail.TooManyPhotos", $"A trail can have at most {Trail.MaxPhotos} photos.");
    public static readonly Error HiddenByModerators = Error.Forbidden("Trail.HiddenByModerators", "This trail was taken out of Discover after reports. A moderator has to review it.");
    public static readonly Error NotModerator = Error.Forbidden("Trail.NotModerator", "Only moderators can do that.");
    public static readonly Error InvalidReport = Error.Validation("Trail.InvalidReport", $"Add details of at most {Community.TrailReport.MaxDetailsLength} characters.");
    public static readonly Error CantReportOwnTrail = Error.Validation("Trail.CantReportOwnTrail", "You can't report your own trail.");
    public static readonly Error InvalidPhotoUrl = Error.Validation("Trail.InvalidPhotoUrl", "Photo URL must be an absolute https URL.");
}
