using Microsoft.EntityFrameworkCore;
using TrailHub.Profile.Application.Interfaces.Repository;
using TrailHub.Profile.Domain.Profiles;
using TrailHub.Profile.Infrastructure.Persistence;

namespace TrailHub.Profile.Infrastructure.Repositories;

internal sealed class ProfileRepository(ProfileDbContext db) : IProfileRepository
{
    public Task<UserProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public void Add(UserProfile profile) => db.Profiles.Add(profile);
}
