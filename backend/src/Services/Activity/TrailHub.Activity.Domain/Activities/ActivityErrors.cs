using TrailHub.BuildingBlocks.Domain;
using TrailHub.BuildingBlocks.Domain.Geo;

namespace TrailHub.Activity.Domain.Activities;

public static class ActivityErrors
{
    public static readonly Error NotFound = Error.NotFound("Activity.NotFound", "The activity does not exist.");
    public static readonly Error MissingId = Error.Validation("Activity.MissingId", "A client-generated activity id is required.");
    public static readonly Error IdTaken = Error.Conflict("Activity.IdTaken", "An activity with this id already exists.");
    public static readonly Error InvalidName = Error.Validation("Activity.InvalidName", $"Name is required and must be at most {HikeActivity.MaxNameLength} characters.");
    public static readonly Error NotesTooLong = Error.Validation("Activity.NotesTooLong", "Notes are too long.");
    public static readonly Error TooFewPoints = Error.Validation("Activity.TooFewPoints", "An activity needs at least two track points.");
    public static readonly Error TooManyPoints = Error.Validation("Activity.TooManyPoints", $"An activity can have at most {HikeActivity.MaxPoints} points.");
    public static readonly Error TimestampsOutOfOrder = Error.Validation("Activity.TimestampsOutOfOrder", "Track point timestamps must not go backwards.");
    public static readonly Error TimestampsInFuture = Error.Validation("Activity.TimestampsInFuture", "Track point timestamps are in the future.");
    public static readonly Error GpxWithoutTimestamps = Error.Validation("Activity.GpxWithoutTimestamps", "Every track point needs a <time> to import a GPX file as an activity. Import it as a trail instead.");
}
