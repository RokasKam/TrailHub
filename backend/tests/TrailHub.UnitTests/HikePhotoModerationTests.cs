using TrailHub.Activity.Domain.Activities;
using TrailHub.Activity.Domain.Feed;

namespace TrailHub.UnitTests;

public sealed class HikePhotoModerationTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private static (HikeActivity Hike, HikePhoto Photo) HikeWithPhoto()
    {
        var points = Enumerable.Range(0, 5).Select(i => new TrackPoint(46 + i * 0.001, 7.5, 1000, Start.AddMinutes(i))).ToList();
        var hike = HikeActivity.Create(Guid.NewGuid(), "author", "Hike", null, [new ActivitySegment(points)], ActivitySource.Recorded, null, Start.AddHours(1)).Value;
        var photo = hike.AddPhoto($"{HikeActivity.PhotoKeyPrefix(hike.Id)}a.jpg", null, Start).Value;
        return (hike, photo);
    }

    [Fact]
    public void The_hikes_author_cant_report_its_photos()
    {
        var (hike, photo) = HikeWithPhoto();

        Assert.Equal(ActivityErrors.CantReportOwnContent, ContentReport.Create(ContentKind.HikePhoto, hike.Id, photo.Id, hike.UserId, "author", ContentReportReason.Spam, null, Start).Error);
        Assert.True(ContentReport.Create(ContentKind.HikePhoto, hike.Id, photo.Id, hike.UserId, "member", ContentReportReason.Spam, null, Start).IsSuccess);
    }

    [Fact]
    public void The_third_open_report_hides_the_photo_from_others()
    {
        var (hike, photo) = HikeWithPhoto();

        Assert.False(hike.ApplyPhotoReports(photo.Id, 2));
        Assert.True(hike.ApplyPhotoReports(photo.Id, 3));
        Assert.Equal(HiddenBy.Reports, hike.Photos.Single().HiddenBy);

        Assert.Equal(ActivityErrors.PhotoNotFound, hike.GetVisiblePhoto(photo.Id, "member", isModerator: false).Error);
        Assert.True(hike.GetVisiblePhoto(photo.Id, "author", isModerator: false).IsSuccess);
        Assert.True(hike.GetVisiblePhoto(photo.Id, "moderator", isModerator: true).IsSuccess);
    }

    [Fact]
    public void Moderators_hide_and_restore_photos()
    {
        var (hike, photo) = HikeWithPhoto();

        Assert.True(hike.ModeratePhoto(photo.Id, hidden: true).IsSuccess);
        Assert.False(hike.ApplyPhotoReports(photo.Id, 3));
        Assert.Equal(HiddenBy.Moderator, hike.Photos.Single().HiddenBy);

        Assert.True(hike.ModeratePhoto(photo.Id, hidden: false).IsSuccess);
        Assert.Null(hike.Photos.Single().HiddenBy);
        Assert.Equal(ActivityErrors.PhotoNotFound, hike.ModeratePhoto(Guid.NewGuid(), hidden: true).Error);
    }

    [Fact]
    public void The_author_and_moderators_can_delete_photos()
    {
        var (hike, _) = HikeWithPhoto();

        Assert.True(hike.CanDeletePhotos("author", isModerator: false));
        Assert.True(hike.CanDeletePhotos("moderator", isModerator: true));
        Assert.False(hike.CanDeletePhotos("member", isModerator: false));
    }
}
