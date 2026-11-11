using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrailHub.Profile.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> create the context at design time (migrations).</summary>
internal sealed class ProfileDbContextFactory : IDesignTimeDbContextFactory<ProfileDbContext>
{
    public ProfileDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProfileDbContext>();
        options.UseProfileNpgsql("Host=localhost;Database=trailhub_profile;Username=trailhub;Password=trailhub");
        return new ProfileDbContext(options.Options);
    }
}
