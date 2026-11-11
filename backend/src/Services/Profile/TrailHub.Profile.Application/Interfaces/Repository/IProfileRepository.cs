using TrailHub.Profile.Domain.Profiles;

namespace TrailHub.Profile.Application.Interfaces.Repository;

public interface IProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    void Add(UserProfile profile);
}
