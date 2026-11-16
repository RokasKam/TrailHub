using TrailHub.BuildingBlocks.Domain;
using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Domain.Devices;

/// <summary>A phone that receives pushes for a user, by its Expo push token. A token belongs to one user at a time.</summary>
public sealed class PushDevice : AggregateRoot<Guid>
{
    public const int MaxTokenLength = 255;

    private PushDevice()
    {
    }

    public string Token { get; private set; } = null!;

    public string UserId { get; private set; } = null!;

    public DevicePlatform Platform { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public static Result<PushDevice> Register(string? token, string userId, string? platform, DateTime nowUtc)
    {
        if (!IsValidToken(token) || ParsePlatform(platform) is not { } parsed)
        {
            return NotificationErrors.InvalidDevice;
        }

        return new PushDevice
        {
            Id = Guid.CreateVersion7(),
            Token = token!,
            UserId = userId,
            Platform = parsed,
            CreatedAtUtc = nowUtc,
            LastSeenAtUtc = nowUtc,
        };
    }

    /// <summary>The app registered this token again, possibly for another user (someone else signed in on the phone).</summary>
    public Result Refresh(string userId, string? platform, DateTime nowUtc)
    {
        if (ParsePlatform(platform) is not { } parsed)
        {
            return NotificationErrors.InvalidDevice;
        }

        UserId = userId;
        Platform = parsed;
        LastSeenAtUtc = nowUtc;
        return Result.Success();
    }

    public static bool IsValidToken(string? token) =>
        !string.IsNullOrEmpty(token) && token.Length <= MaxTokenLength && !token.Any(char.IsWhiteSpace);

    /// <summary><c>ios</c> or <c>android</c>, in any case.</summary>
    public static DevicePlatform? ParsePlatform(string? platform) =>
        platform?.Trim().ToUpperInvariant() switch
        {
            "IOS" => DevicePlatform.Ios,
            "ANDROID" => DevicePlatform.Android,
            _ => null,
        };
}
