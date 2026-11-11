using TrailHub.BuildingBlocks.Application;
using TrailHub.Profile.Application.Interfaces.Repository;
using TrailHub.Profile.Application.Interfaces.Service;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Services;

internal sealed class ProfileProvisioner(IProfileRepository profiles, IUnitOfWork unitOfWork, TimeProvider clock) : IProfileProvisioner
{
    public async Task<UserProfile> GetOrCreateAsync(string userId, string? displayName, string? email, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = UserProfile.Create(userId, displayName, email, clock.GetUtcNow().UtcDateTime);
        profiles.Add(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile;
    }
}
