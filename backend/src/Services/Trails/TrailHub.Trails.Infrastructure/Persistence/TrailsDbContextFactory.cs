using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrailHub.Trails.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only; runtime configuration comes from the API host.</summary>
internal sealed class TrailsDbContextFactory : IDesignTimeDbContextFactory<TrailsDbContext>
{
    public TrailsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TrailsDbContext>();
        options.UseTrailsNpgsql("Host=localhost;Database=trailhub_trails;Username=trailhub;Password=trailhub");
        return new TrailsDbContext(options.Options);
    }
}
