using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trails.Domain.Community;

public static class CommunityErrors
{
    public static readonly Error InvalidRating = Error.Validation("Review.InvalidRating", "Rating must be between 1 and 5.");
    public static readonly Error CommentTooLong = Error.Validation("Review.CommentTooLong", $"Comment must be at most {Review.MaxCommentLength} characters.");
    public static readonly Error NoteTooLong = Error.Validation("ConditionReport.NoteTooLong", $"Note must be at most {ConditionReport.MaxNoteLength} characters.");
}
