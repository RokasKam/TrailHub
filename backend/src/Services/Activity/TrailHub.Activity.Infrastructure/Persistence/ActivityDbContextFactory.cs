using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrailHub.Activity.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> create the context at design time (migrations).</summary>
internal sealed class ActivityDbContextFactory : IDesignTimeDbContextFactory<ActivityDbContext>
{
    public ActivityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ActivityDbContext>();
        options.UseActivityNpgsql("Host=localhost;Database=trailhub_activity;Username=trailhub;Password=trailhub");
        return new ActivityDbContext(options.Options);
    }
}
