using TrailHub.Profile.Application.Requests;
using TrailHub.Profile.Application.Responses;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Interfaces.Service;

/// <summary>The signed-in user's profile, preferences and achievements. The profile is created on first use.</summary>
public interface IProfileService
{
    Task<ProfileResponse> GetMyProfileAsync(CancellationToken cancellationToken);

    Task<ProfileResponse> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<ProfileResponse> UpdatePreferencesAsync(ProfilePreferences preferences, CancellationToken cancellationToken);

    Task<IReadOnlyList<AchievementResponse>> GetMyAchievementsAsync(CancellationToken cancellationToken);
}
