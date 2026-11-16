using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.UnitTests;

public sealed class NotificationLikeBatchTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Hike = Guid.NewGuid();

    [Fact]
    public void First_like_names_the_liker()
    {
        var n = Notification.ForLike("anna", Hike, "Rysy ridge", "ben", "Ben", Start);

        Assert.Equal("Ben liked your hike Rysy ridge", n.Body);
        Assert.Equal(NotificationCategory.LikesComments, n.Category);
        Assert.Equal($"/feed/{Hike}", n.TargetUrl);
        Assert.Equal($"like:{Hike}", n.GroupKey);
        Assert.Equal(1, n.Count);
        Assert.False(n.IsRead);
    }

    [Fact]
    public void Later_likes_name_the_latest_and_count_the_rest()
    {
        var n = Notification.ForLike("anna", Hike, "Rysy ridge", "cleo", "Cleo", Start);
        n.AddLike("dan", "Dan", "Rysy ridge", Start.AddMinutes(1));
        Assert.Equal("Dan and 1 other liked your hike Rysy ridge", n.Body);

        n.AddLike("ben", "Ben", "Rysy ridge", Start.AddMinutes(2));

        Assert.Equal("Ben and 2 others liked your hike Rysy ridge", n.Body);
        Assert.Equal(3, n.Count);
        Assert.Equal(["cleo", "dan", "ben"], n.ActorIds);
        Assert.Equal(Start.AddMinutes(2), n.UpdatedAtUtc);
        Assert.Equal(Start, n.CreatedAtUtc);
    }

    [Fact]
    public void A_new_like_makes_the_item_unread_again()
    {
        var n = Notification.ForLike("anna", Hike, "Rysy ridge", "ben", "Ben", Start);
        n.MarkRead(Start.AddMinutes(1));
        Assert.True(n.IsRead);

        n.AddLike("cleo", "Cleo", "Rysy ridge", Start.AddMinutes(2));

        Assert.False(n.IsRead);
    }

    [Fact]
    public void Someone_already_counted_changes_nothing()
    {
        var n = Notification.ForLike("anna", Hike, "Rysy ridge", "ben", "Ben", Start);
        n.MarkRead(Start.AddMinutes(1));

        Assert.False(n.AddLike("ben", "Ben", "Rysy ridge", Start.AddMinutes(2)));
        Assert.Equal(1, n.Count);
        Assert.True(n.IsRead);
    }

    [Fact]
    public void Batched_likes_push_at_most_every_ten_minutes()
    {
        var n = Notification.ForLike("anna", Hike, "Rysy ridge", "ben", "Ben", Start);
        Assert.True(n.CanPush(Start));
        n.MarkPushed(Start);

        Assert.False(n.CanPush(Start.AddMinutes(9).AddSeconds(59)));
        Assert.True(n.CanPush(Start.AddMinutes(10)));
    }

    [Fact]
    public void Single_events_always_push()
    {
        var n = Notification.Create("anna", NotificationKind.HikeCommented, "New comment", "Ben commented", "/feed/x", "ben", "comment:1", Start);
        n.MarkPushed(Start);

        Assert.True(n.CanPush(Start.AddSeconds(1)));
    }

    [Theory]
    [InlineData(NotificationKind.HikeLiked, NotificationCategory.LikesComments)]
    [InlineData(NotificationKind.HikeCommented, NotificationCategory.LikesComments)]
    [InlineData(NotificationKind.HikeShared, NotificationCategory.Group)]
    [InlineData(NotificationKind.GroupHikeScheduled, NotificationCategory.Group)]
    [InlineData(NotificationKind.GroupMemberJoined, NotificationCategory.Group)]
    [InlineData(NotificationKind.TripExpenseAdded, NotificationCategory.Trips)]
    [InlineData(NotificationKind.TripPaymentRecorded, NotificationCategory.Trips)]
    [InlineData(NotificationKind.TripGearUnclaimed, NotificationCategory.Trips)]
    public void Kinds_belong_to_categories(NotificationKind kind, NotificationCategory category) =>
        Assert.Equal(category, Notification.CategoryOf(kind));
}
