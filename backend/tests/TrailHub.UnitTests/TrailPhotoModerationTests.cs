using TrailHub.BuildingBlocks.Domain.Geo;
using TrailHub.Trails.Domain.Community;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.UnitTests;

public sealed class TrailPhotoModerationTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private static (Trail Trail, TrailPhoto Photo) TrailWithPhoto()
    {
        var trail = Trail.Create("Ridge", null, "Alps", Difficulty.Moderate, [new GeoPoint(46, 7, 1000), new GeoPoint(46.01, 7, 1100)], "author", Now).Value;
        var photo = trail.AddUploadedPhoto($"{Trail.PhotoKeyPrefix(trail.Id)}a.jpg", "Summit", "uploader", Now).Value;
        return (trail, photo);
    }

    [Fact]
    public void Photos_get_a_stable_id()
    {
        var (trail, photo) = TrailWithPhoto();

        Assert.NotEqual(Guid.Empty, photo.Id);
        Assert.Equal(photo.Id, trail.Photos.Single().Id);
        Assert.True(photo.IsStored);
    }

    [Fact]
    public void Uploaders_cant_report_their_own_photo()
    {
        var (trail, photo) = TrailWithPhoto();

        Assert.Equal(TrailErrors.CantReportOwnContent, PhotoReport.Create(trail.Id, photo, "uploader", ContentReportReason.Spam, null, Now).Error);
        Assert.True(PhotoReport.Create(trail.Id, photo, "author", ContentReportReason.Harassment, "Not ok", Now).IsSuccess);
    }

    [Fact]
    public void Report_details_are_limited()
    {
        var (trail, photo) = TrailWithPhoto();

        Assert.Equal(TrailErrors.InvalidReport, PhotoReport.Create(trail.Id, photo, "someone", ContentReportReason.Other, new string('x', 1001), Now).Error);
        Assert.Equal(TrailErrors.InvalidReport, PhotoReport.Create(trail.Id, photo, "someone", (ContentReportReason)42, null, Now).Error);
    }

    [Fact]
    public void Reporting_again_reopens_the_report()
    {
        var (trail, photo) = TrailWithPhoto();
        var report = PhotoReport.Create(trail.Id, photo, "someone", ContentReportReason.Spam, null, Now).Value;
        report.Resolve(Now);

        Assert.True(report.Update(ContentReportReason.Inappropriate, " Rude ", Now.AddHours(1)).IsSuccess);
        Assert.True(report.IsOpen);
        Assert.Equal("Rude", report.Details);
    }

    [Fact]
    public void The_third_open_report_hides_the_photo_from_others()
    {
        var (trail, photo) = TrailWithPhoto();

        Assert.False(trail.ApplyPhotoReports(photo.Id, 2));
        Assert.False(photo.IsHidden);
        Assert.True(trail.ApplyPhotoReports(photo.Id, 3));
        Assert.Equal(HiddenBy.Reports, photo.HiddenBy);

        Assert.Equal(TrailErrors.PhotoNotFound, trail.GetVisiblePhoto(photo.Id, "someone", isModerator: false).Error);
        Assert.True(trail.GetVisiblePhoto(photo.Id, "uploader", isModerator: false).IsSuccess);
        Assert.True(trail.GetVisiblePhoto(photo.Id, "moderator", isModerator: true).IsSuccess);
    }

    [Fact]
    public void Reports_dont_override_a_moderators_hide()
    {
        var (trail, photo) = TrailWithPhoto();
        trail.ModeratePhoto(photo.Id, hidden: true);

        Assert.False(trail.ApplyPhotoReports(photo.Id, 5));
        Assert.Equal(HiddenBy.Moderator, photo.HiddenBy);
    }

    [Fact]
    public void Moderators_restore_hidden_photos()
    {
        var (trail, photo) = TrailWithPhoto();
        trail.ApplyPhotoReports(photo.Id, 3);

        Assert.True(trail.ModeratePhoto(photo.Id, hidden: false).IsSuccess);
        Assert.False(photo.IsHidden);
        Assert.Equal(TrailErrors.PhotoNotFound, trail.ModeratePhoto(Guid.NewGuid(), hidden: false).Error);
    }

    [Theory]
    [InlineData("uploader", false, true)]
    [InlineData("author", false, true)]
    [InlineData("moderator", true, true)]
    [InlineData("someone", false, false)]
    public void Uploaders_trail_authors_and_moderators_can_delete_a_photo(string userId, bool isModerator, bool allowed)
    {
        var (trail, photo) = TrailWithPhoto();

        var result = trail.RemovePhoto(photo.Id, userId, isModerator);

        Assert.Equal(allowed, result.IsSuccess);
        Assert.Equal(allowed ? null : TrailErrors.NotPhotoOwner, result.Error);
        Assert.Equal(allowed ? 0 : 1, trail.Photos.Count);
    }

    [Fact]
    public void Deleting_a_missing_or_unseen_photo_is_not_found()
    {
        var (trail, photo) = TrailWithPhoto();
        Assert.Equal(TrailErrors.PhotoNotFound, trail.RemovePhoto(Guid.NewGuid(), "author", isModerator: false).Error);

        trail.ApplyPhotoReports(photo.Id, 3);
        Assert.Equal(TrailErrors.PhotoNotFound, trail.RemovePhoto(photo.Id, "someone", isModerator: false).Error);
    }
}
