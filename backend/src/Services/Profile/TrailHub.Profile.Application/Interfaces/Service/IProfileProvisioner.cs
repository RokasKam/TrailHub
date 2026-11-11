using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Interfaces.Service;

/// <summary>Loads a user's profile, creating it on first sign-in.</summary>
internal interface IProfileProvisioner
{
    Task<UserProfile> GetOrCreateAsync(string userId, string? displayName, string? email, CancellationToken cancellationToken);
}
