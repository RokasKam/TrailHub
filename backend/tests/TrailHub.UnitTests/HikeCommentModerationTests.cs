using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;
using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.UnitTests;

public sealed class HikeCommentModerationTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private static HikeComment Comment() => HikeComment.Create(Guid.NewGuid(), "commenter", "hi", Now).Value;

    private static Result<ContentReport> Report(HikeComment c, string userId, string? details = null) =>
        ContentReport.Create(ContentKind.Comment, c.ActivityId, c.Id, c.UserId, userId, ContentReportReason.Harassment, details, Now);

    [Fact]
    public void Writers_cant_report_their_own_comment()
    {
        var comment = Comment();

        Assert.Equal(ActivityErrors.CantReportOwnContent, Report(comment, "commenter").Error);
        Assert.False(comment.CanBeReportedBy("commenter"));
        Assert.True(comment.CanBeReportedBy("someone"));
        Assert.True(Report(comment, "someone").IsSuccess);
    }

    [Fact]
    public void Report_details_are_limited()
    {
        Assert.Equal(ActivityErrors.InvalidReport, Report(Comment(), "someone", new string('x', 1001)).Error);
    }

    [Fact]
    public void The_third_open_report_hides_the_comment_from_others()
    {
        var comment = Comment();

        Assert.False(comment.ApplyReports(2));
        Assert.True(comment.IsVisibleTo("someone", isModerator: false));
        Assert.True(comment.ApplyReports(3));
        Assert.Equal(HiddenBy.Reports, comment.HiddenBy);

        Assert.False(comment.IsVisibleTo("someone", isModerator: false));
        Assert.True(comment.IsVisibleTo("commenter", isModerator: false));
        Assert.True(comment.IsVisibleTo("moderator", isModerator: true));
    }

    [Fact]
    public void Moderators_hide_and_restore_and_reports_dont_override_them()
    {
        var comment = Comment();
        comment.Moderate(hidden: true);
        Assert.False(comment.ApplyReports(3));
        Assert.Equal(HiddenBy.Moderator, comment.HiddenBy);

        comment.Moderate(hidden: false);
        Assert.False(comment.IsHidden);
    }

    [Fact]
    public void Moderators_can_delete_any_comment()
    {
        var comment = Comment();

        Assert.True(comment.CanBeDeletedBy("moderator", "author", isModerator: true));
        Assert.False(comment.CanBeDeletedBy("moderator", "author", isModerator: false));
    }
}
