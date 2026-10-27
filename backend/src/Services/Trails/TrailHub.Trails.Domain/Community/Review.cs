using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trails.Domain.Community;

/// <summary>One review per user per trail; posting again updates it.</summary>
public sealed class Review : AggregateRoot<Guid>
{
    public const int MaxCommentLength = 4000;

    private Review()
    {
    }

    public Guid TrailId { get; private set; }

    public string UserId { get; private set; } = null!;

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public static Result<Review> Create(Guid trailId, string userId, int rating, string? comment, DateTime nowUtc)
    {
        if (Validate(rating, comment) is { } error)
        {
            return error;
        }

        return new Review
        {
            Id = Guid.CreateVersion7(),
            TrailId = trailId,
            UserId = userId,
            Rating = rating,
            Comment = Normalize(comment),
            CreatedAtUtc = nowUtc,
        };
    }

    public Result Update(int rating, string? comment, DateTime nowUtc)
    {
        if (Validate(rating, comment) is { } error)
        {
            return error;
        }

        Rating = rating;
        Comment = Normalize(comment);
        UpdatedAtUtc = nowUtc;
        return Result.Success();
    }

    private static Error? Validate(int rating, string? comment) =>
        rating is < 1 or > 5 ? CommunityErrors.InvalidRating
        : comment?.Length > MaxCommentLength ? CommunityErrors.CommentTooLong
        : null;

    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

public enum TrailCondition
{
    Good,
    Muddy,
    Snow,
    Ice,
    Flooded,
    Overgrown,
    Blocked,
    Closed,
}

/// <summary>A dated, user-submitted snapshot of on-the-ground conditions.</summary>
public sealed class ConditionReport : AggregateRoot<Guid>
{
    public const int MaxNoteLength = 2000;

    private ConditionReport()
    {
    }

    public Guid TrailId { get; private set; }

    public string UserId { get; private set; } = null!;

    public TrailCondition Condition { get; private set; }

    public string? Note { get; private set; }

    public DateTime ReportedAtUtc { get; private set; }

    public static Result<ConditionReport> Create(Guid trailId, string userId, TrailCondition condition, string? note, DateTime nowUtc)
    {
        if (note?.Length > MaxNoteLength)
        {
            return CommunityErrors.NoteTooLong;
        }

        return new ConditionReport
        {
            Id = Guid.CreateVersion7(),
            TrailId = trailId,
            UserId = userId,
            Condition = condition,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ReportedAtUtc = nowUtc,
        };
    }
}

public sealed class FavoriteTrail
{
    private FavoriteTrail()
    {
    }

    public string UserId { get; private set; } = null!;

    public Guid TrailId { get; private set; }

    public DateTime SavedAtUtc { get; private set; }

    public static FavoriteTrail Create(string userId, Guid trailId, DateTime nowUtc) =>
        new() { UserId = userId, TrailId = trailId, SavedAtUtc = nowUtc };
}

public static class CommunityErrors
{
    public static readonly Error InvalidRating = Error.Validation("Review.InvalidRating", "Rating must be between 1 and 5.");
    public static readonly Error CommentTooLong = Error.Validation("Review.CommentTooLong", $"Comment must be at most {Review.MaxCommentLength} characters.");
    public static readonly Error NoteTooLong = Error.Validation("ConditionReport.NoteTooLong", $"Note must be at most {ConditionReport.MaxNoteLength} characters.");
}
