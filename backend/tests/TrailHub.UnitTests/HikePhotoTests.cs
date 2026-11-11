using TrailHub.Activity.Domain.Activities;

namespace TrailHub.UnitTests;

public sealed class HikePhotoTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Adds_and_removes_photos()
    {
        var hike = Hike();

        var photo = hike.AddPhoto($"{HikeActivity.PhotoKeyPrefix(hike.Id)}a.jpg", " summit ", Start).Value;
        Assert.Equal("summit", photo.Caption);
        Assert.Single(hike.Photos);

        Assert.Equal(photo, hike.RemovePhoto(photo.Id).Value);
        Assert.Empty(hike.Photos);
    }

    [Fact]
    public void At_most_six_photos()
    {
        var hike = Hike();
        for (var i = 0; i < HikeActivity.MaxPhotos; i++)
        {
            Assert.True(hike.AddPhoto($"{HikeActivity.PhotoKeyPrefix(hike.Id)}{i}.jpg", null, Start).IsSuccess);
        }

        Assert.Equal(ActivityErrors.TooManyPhotos, hike.AddPhoto($"{HikeActivity.PhotoKeyPrefix(hike.Id)}x.jpg", null, Start).Error);
    }

    [Fact]
    public void Photo_keys_must_belong_to_the_hike()
    {
        var hike = Hike();

        Assert.Equal(ActivityErrors.InvalidPhotoKey, hike.AddPhoto($"{HikeActivity.PhotoKeyPrefix(Guid.NewGuid())}a.jpg", null, Start).Error);
        Assert.Equal(ActivityErrors.InvalidPhotoKey, hike.AddPhoto($"{HikeActivity.PhotoKeyPrefix(hike.Id)}../a.jpg", null, Start).Error);
    }

    [Fact]
    public void Removing_an_unknown_photo_fails()
    {
        Assert.Equal(ActivityErrors.PhotoNotFound, Hike().RemovePhoto(Guid.NewGuid()).Error);
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
