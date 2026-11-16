using TrailHub.Notifications.Domain.Devices;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.UnitTests;

public sealed class PushDeviceTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("ios", DevicePlatform.Ios)]
    [InlineData("Android", DevicePlatform.Android)]
    public void Registers_with_a_platform(string platform, DevicePlatform expected)
    {
        var device = PushDevice.Register("ExponentPushToken[abc]", "anna", platform, Now);

        Assert.True(device.IsSuccess);
        Assert.Equal(expected, device.Value.Platform);
        Assert.Equal("anna", device.Value.UserId);
    }

    [Theory]
    [InlineData(null, "ios")]
    [InlineData("", "ios")]
    [InlineData("has space", "ios")]
    [InlineData("ExponentPushToken[abc]", "windows")]
    public void Rejects_bad_tokens_and_platforms(string? token, string platform) =>
        Assert.Equal(NotificationErrors.InvalidDevice, PushDevice.Register(token, "anna", platform, Now).Error);

    [Fact]
    public void A_token_moves_to_whoever_registers_it()
    {
        var device = PushDevice.Register("ExponentPushToken[abc]", "anna", "ios", Now).Value;

        device.Refresh("ben", "ios", Now.AddDays(1));

        Assert.Equal("ben", device.UserId);
        Assert.Equal(Now.AddDays(1), device.LastSeenAtUtc);
    }
}
