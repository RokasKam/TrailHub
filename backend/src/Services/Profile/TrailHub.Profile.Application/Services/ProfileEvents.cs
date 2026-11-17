using TrailHub.BuildingBlocks.Application;
using TrailHub.Contracts;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Services;

internal static class ProfileEvents
{
    /// <summary>Lets Groups (and through its member lists, every other service) show the profile's current name.</summary>
    public static Task PublishRenamedAsync(this IEventPublisher events, UserProfile profile, CancellationToken cancellationToken) =>
        events.PublishAsync(new ProfileRenamedIntegrationEvent(profile.UserId, profile.DisplayName), cancellationToken);
}
