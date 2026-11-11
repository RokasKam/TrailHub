using TrailHub.Activity.Domain.Activities;

namespace TrailHub.UnitTests;

public sealed class HikeSharingTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid GroupA = Guid.NewGuid();
    private static readonly Guid GroupB = Guid.NewGuid();
    private static readonly Guid[] AuthorGroups = [GroupA, GroupB];

    [Fact]
    public void Sharing_sets_groups_caption_and_privacy()
    {
        var hike = Hike();

        var result = hike.Share([GroupA, GroupA], AuthorGroups, "  Great day  ", TrackPrivacy.HideEnds, Start.AddDays(1));

        Assert.True(result.IsSuccess);
        Assert.Equal([GroupA], hike.SharedGroupIds);
        Assert.Equal("Great day", hike.Caption);
        Assert.Equal(TrackPrivacy.HideEnds, hike.Privacy);
        Assert.Equal(Start.AddDays(1), hike.SharedAtUtc);
    }

    [Fact]
    public void Resharing_keeps_the_original_share_time()
    {
        var hike = Hike();
        hike.Share([GroupA], AuthorGroups, null, TrackPrivacy.Full, Start.AddDays(1));

        hike.Share([GroupB], AuthorGroups, "edited", TrackPrivacy.StatsOnly, Start.AddDays(2));

        Assert.Equal([GroupB], hike.SharedGroupIds);
        Assert.Equal("edited", hike.Caption);
        Assert.Equal(Start.AddDays(1), hike.SharedAtUtc);
    }

    [Fact]
    public void Needs_at_least_one_group()
    {
        Assert.Equal(ActivityErrors.NoGroups, Hike().Share([], AuthorGroups, null, TrackPrivacy.Full, Start.AddDays(1)).Error);
    }

    [Fact]
    public void At_most_ten_groups()
    {
        var groups = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray();

        Assert.Equal(ActivityErrors.TooManyGroups, Hike().Share(groups, groups, null, TrackPrivacy.Full, Start.AddDays(1)).Error);
    }

    [Fact]
    public void Only_groups_the_author_belongs_to()
    {
        var result = Hike().Share([GroupA, Guid.NewGuid()], AuthorGroups, null, TrackPrivacy.Full, Start.AddDays(1));

        Assert.Equal(ActivityErrors.NotAGroupMember, result.Error);
    }

    [Fact]
    public void Caption_is_limited()
    {
        var result = Hike().Share([GroupA], AuthorGroups, new string('x', HikeActivity.MaxCaptionLength + 1), TrackPrivacy.Full, Start.AddDays(1));

        Assert.Equal(ActivityErrors.InvalidCaption, result.Error);
    }

    [Fact]
    public void Rejects_unknown_privacy()
    {
        Assert.Equal(ActivityErrors.InvalidPrivacy, Hike().Share([GroupA], AuthorGroups, null, (TrackPrivacy)42, Start.AddDays(1)).Error);
    }

    [Fact]
    public void Visible_to_the_author_and_members_of_a_shared_group_only()
    {
        var hike = Hike();
        Assert.False(hike.IsVisibleTo("author", AuthorGroups)); // not shared yet

        hike.Share([GroupA], AuthorGroups, null, TrackPrivacy.Full, Start.AddDays(1));

        Assert.True(hike.IsVisibleTo("author", []));
        Assert.True(hike.IsVisibleTo("member", [GroupA]));
        Assert.False(hike.IsVisibleTo("leaver", [GroupB]));
        Assert.False(hike.IsVisibleTo("stranger", []));
    }

    [Fact]
    public void Unsharing_hides_it_and_clears_sharing()
    {
        var hike = Hike();
        hike.Share([GroupA], AuthorGroups, "c", TrackPrivacy.HideEnds, Start.AddDays(1));

        hike.Unshare();

        Assert.False(hike.IsShared);
        Assert.Empty(hike.SharedGroupIds);
        Assert.Null(hike.Caption);
        Assert.False(hike.IsVisibleTo("member", [GroupA]));
    }

    private static HikeActivity Hike() =>
        HikeActivity.Create(
            Guid.NewGuid(),
            "author",
            "Hike",
            null,
            [new ActivitySegment([new(45, 10, null, Start), new(45.01, 10, null, Start.AddHours(1))])],
            ActivitySource.Recorded,
            null,
            Start.AddDays(1)).Value;
}
