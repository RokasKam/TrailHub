using TrailHub.BuildingBlocks.Application;
using TrailHub.Profile.Application.Interfaces.Repository;
using TrailHub.Profile.Application.Interfaces.Service;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Services;

internal sealed class ProfileProvisioner(IProfileRepository profiles, IUnitOfWork unitOfWork, IEventPublisher events, TimeProvider clock) : IProfileProvisioner
{
    public async Task<UserProfile> GetOrCreateAsync(string userId, string? displayName, string? email, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken);
        if (profile is not null)
        {
            // Profiles created before names were made readable still have their email as their name.
            if (profile.FixEmailDisplayName())
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await events.PublishRenamedAsync(profile, cancellationToken);
            }

            return profile;
        }

        profile = UserProfile.Create(userId, displayName, email, clock.GetUtcNow().UtcDateTime);
        profiles.Add(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // A profile created from a hike event has no name to go on ("Hiker"); that placeholder must not replace
        // the names the person gave their groups.
        if (displayName is not null || email is not null)
        {
            await events.PublishRenamedAsync(profile, cancellationToken);
        }

        return profile;
    }
}
