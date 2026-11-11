using TrailHub.Activity.Domain.Feed;
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
    public static readonly Error NoGroups = Error.Validation("Activity.NoGroups", "Pick at least one group to share the hike with.");
    public static readonly Error TooManyGroups = Error.Validation("Activity.TooManyGroups", $"A hike can be shared with at most {HikeActivity.MaxShareGroups} groups.");
    public static readonly Error NotAGroupMember = Error.Validation("Activity.NotAGroupMember", "You can only share a hike with groups you belong to.");
    public static readonly Error InvalidCaption = Error.Validation("Activity.InvalidCaption", $"A caption must be at most {HikeActivity.MaxCaptionLength} characters.");
    public static readonly Error InvalidPrivacy = Error.Validation("Activity.InvalidPrivacy", "Privacy must be Full, HideEnds or StatsOnly.");
    public static readonly Error TooManyPhotos = Error.Validation("Activity.TooManyPhotos", $"A hike can have at most {HikeActivity.MaxPhotos} photos.");
    public static readonly Error UnsupportedPhoto = Error.Validation("Activity.UnsupportedPhoto", "Photos must be JPEG, PNG, HEIC or WebP images.");
    public static readonly Error PhotoTooLarge = Error.Validation("Activity.PhotoTooLarge", "Photos must be at most 10 MB.");
    public static readonly Error InvalidPhotoKey = Error.Validation("Activity.InvalidPhotoKey", "The photo doesn't belong to this hike.");
    public static readonly Error PhotoNotFound = Error.NotFound("Activity.PhotoNotFound", "The photo does not exist.");
    public static readonly Error GroupNotFound = Error.NotFound("Activity.GroupNotFound", "You're not a member of that group.");
    public static readonly Error InvalidComment = Error.Validation("Activity.InvalidComment", $"A comment must be 1 to {HikeComment.MaxTextLength} characters.");
    public static readonly Error CommentNotFound = Error.NotFound("Activity.CommentNotFound", "The comment does not exist.");
    public static readonly Error CantReportOwnContent = Error.Validation("Activity.CantReportOwnContent", "You can't report your own comments or photos.");
    public static readonly Error InvalidReport = Error.Validation("Activity.InvalidReport", $"Pick a reason and add details of at most {ContentReport.MaxDetailsLength} characters.");
    public static readonly Error NotModerator = Error.Forbidden("Activity.NotModerator", "Only moderators can do that.");
    public static readonly Error InvalidModerationAction = Error.Validation("Activity.InvalidModerationAction", "Action must be Restore, Hide or Delete.");
    public static readonly Error NotCommentAuthor = Error.Forbidden("Activity.NotCommentAuthor", "Only the comment's author, the hike's author or a moderator can delete it.");
}
