using TrailHub.BuildingBlocks.Application;
using TrailHub.Profile.Application.Interfaces.Repository;
using TrailHub.Profile.Application.Interfaces.Service;

namespace TrailHub.Profile.Application.Services;

internal sealed class ProfileHikeService(
    IProfileProvisioner provisioner,
    IProfileRepository profiles,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : IProfileHikeService
{
    public async Task<IReadOnlyList<string>> RecordCompletedHikeAsync(
        string userId,
        Guid activityId,
        double distanceMeters,
        double elevationGainMeters,
        DateTime completedAtUtc,
        CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(userId, null, null, cancellationToken);
        var unlocked = profile.RecordHike(activityId, distanceMeters, elevationGainMeters, completedAtUtc, clock.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return unlocked.Select(a => a.Code).ToList();
    }

    public async Task RemoveHikeAsync(string userId, Guid activityId, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(userId, cancellationToken);
        if (profile is not null)
        {
            profile.RemoveHike(activityId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
