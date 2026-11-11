using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Profile.Domain.Profiles;

public static class ProfileErrors
{
    public static readonly Error InvalidDisplayName = Error.Validation("Profile.InvalidDisplayName", $"Display name is required and must be at most {UserProfile.MaxDisplayNameLength} characters.");
    public static readonly Error BioTooLong = Error.Validation("Profile.BioTooLong", $"Bio must be at most {UserProfile.MaxBioLength} characters.");
    public static readonly Error InvalidAvatarUrl = Error.Validation("Profile.InvalidAvatarUrl", "Avatar URL must be an absolute https URL.");
}
