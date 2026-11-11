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
