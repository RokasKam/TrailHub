using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;

namespace TrailHub.UnitTests;

public sealed class HikeCommentTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Text_is_trimmed()
    {
        var comment = HikeComment.Create(Guid.NewGuid(), "u", "  Nice!  ", Now).Value;

        Assert.Equal("Nice!", comment.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_is_rejected(string? text)
    {
        Assert.Equal(ActivityErrors.InvalidComment, HikeComment.Create(Guid.NewGuid(), "u", text, Now).Error);
    }

    [Fact]
    public void Text_is_limited_to_1000_characters()
    {
        Assert.True(HikeComment.Create(Guid.NewGuid(), "u", new string('x', 1000), Now).IsSuccess);
        Assert.Equal(ActivityErrors.InvalidComment, HikeComment.Create(Guid.NewGuid(), "u", new string('x', 1001), Now).Error);
    }

    [Fact]
    public void The_commenter_and_the_hike_author_can_delete_it()
    {
        var comment = HikeComment.Create(Guid.NewGuid(), "commenter", "hi", Now).Value;

        Assert.True(comment.CanBeDeletedBy("commenter", "author"));
        Assert.True(comment.CanBeDeletedBy("author", "author"));
        Assert.False(comment.CanBeDeletedBy("someone-else", "author"));
    }
}
