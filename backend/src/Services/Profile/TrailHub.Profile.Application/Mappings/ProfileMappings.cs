using TrailHub.Profile.Application.Responses;
using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Mappings;

internal static class ProfileMappings
{
    public static ProfileResponse ToResponse(this UserProfile p) =>
        new(p.Id, p.UserId, p.DisplayName, p.Email, p.Bio, p.HomeRegion, p.AvatarUrl, p.Preferences, p.Totals, p.Achievements.Count, p.CreatedAtUtc);
}
