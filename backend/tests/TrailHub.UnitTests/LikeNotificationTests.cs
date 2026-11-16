using TrailHub.Contracts;

namespace TrailHub.UnitTests;

public sealed class LikeNotificationTests
{
    private static readonly Guid Hike = Guid.NewGuid();

    private static HikeLikedIntegrationEvent Liked(string likerId, string likerName) => new(Hike, "anna", "Rysy ridge", likerId, likerName);

    [Fact]
    public async Task Likes_on_one_hike_share_one_inbox_item()
    {
        var h = new NotificationsHarness();

        await h.Feed.HikeLikedAsync(Liked("ben", "Ben"), default);
        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await h.Feed.HikeLikedAsync(Liked("cleo", "Cleo"), default);
        await h.Feed.HikeLikedAsync(Liked("dan", "Dan"), default);

        var item = Assert.Single(h.InboxOf("anna"));
        Assert.Equal("Dan and 2 others liked your hike Rysy ridge", item.Body);
        Assert.Equal(3, item.Count);
    }

    [Fact]
    public async Task At_most_one_push_per_hike_every_ten_minutes()
    {
        var h = new NotificationsHarness();
        h.AddDevice("anna");

        await h.Feed.HikeLikedAsync(Liked("ben", "Ben"), default);
        h.Clock.Advance(TimeSpan.FromMinutes(5));
        await h.Feed.HikeLikedAsync(Liked("cleo", "Cleo"), default);
        Assert.Equal(["Ben liked your hike Rysy ridge"], h.Sender.Sent.Select(m => m.Body));

        h.Clock.Advance(TimeSpan.FromMinutes(5));
        await h.Feed.HikeLikedAsync(Liked("dan", "Dan"), default);

        Assert.Equal(2, h.Sender.Sent.Count);
        Assert.Equal("Dan and 2 others liked your hike Rysy ridge", h.Sender.Sent[1].Body);
        Assert.Equal($"/feed/{Hike}", h.Sender.Sent[1].Url);
    }

    [Fact]
    public async Task A_read_batch_becomes_unread_with_the_next_like()
    {
        var h = new NotificationsHarness();
        await h.Feed.HikeLikedAsync(Liked("ben", "Ben"), default);
        h.InboxOf("anna")[0].MarkRead(h.Clock.Now);

        h.Clock.Advance(TimeSpan.FromMinutes(1));
        await h.Feed.HikeLikedAsync(Liked("cleo", "Cleo"), default);

        Assert.False(h.InboxOf("anna")[0].IsRead);
        Assert.Equal(h.Clock.Now, h.InboxOf("anna")[0].UpdatedAtUtc);
    }

    [Fact]
    public async Task The_badge_is_the_unread_count()
    {
        var h = new NotificationsHarness();
        h.AddDevice("anna");

        await h.Feed.HikeCommentedAsync(new HikeCommentedIntegrationEvent(Hike, "anna", "Rysy ridge", Guid.NewGuid(), "ben", "Ben", "Nice"), default);
        await h.Feed.HikeLikedAsync(Liked("ben", "Ben"), default);

        Assert.Equal([1, 2], h.Sender.Sent.Select(m => m.Badge));
    }
}
