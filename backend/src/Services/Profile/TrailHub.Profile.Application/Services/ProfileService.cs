using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Profile.Application.Interfaces.Service;
using TrailHub.Profile.Application.Mappings;
using TrailHub.Profile.Application.Requests;
using TrailHub.Profile.Application.Responses;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Services;

internal sealed class ProfileService(IProfileProvisioner provisioner, IUnitOfWork unitOfWork, ICurrentUser user) : IProfileService
{
    public async Task<ProfileResponse> GetMyProfileAsync(CancellationToken cancellationToken) =>
        (await provisioner.GetOrCreateAsync(user.UserId, user.Name, user.Email, cancellationToken)).ToResponse();

    public async Task<ProfileResponse> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(user.UserId, request.DisplayName, user.Email, cancellationToken);
        profile.UpdateDetails(request.DisplayName, request.Bio, request.HomeRegion, request.AvatarUrl).ThrowIfFailure();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.ToResponse();
    }

    public async Task<ProfileResponse> UpdatePreferencesAsync(ProfilePreferences preferences, CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(user.UserId, user.Name, user.Email, cancellationToken);
        profile.UpdatePreferences(preferences);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.ToResponse();
    }

    public async Task<IReadOnlyList<AchievementResponse>> GetMyAchievementsAsync(CancellationToken cancellationToken)
    {
        var profile = await provisioner.GetOrCreateAsync(user.UserId, user.Name, user.Email, cancellationToken);
        return AchievementCatalog.All
            .Select(a =>
            {
                var unlocked = profile.Achievements.FirstOrDefault(u => u.Code == a.Code);
                return new AchievementResponse(a.Code, a.Title, a.Description, unlocked is not null, unlocked?.UnlockedAtUtc);
            })
            .ToList();
    }
}
