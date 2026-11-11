using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrailHub.Trips.Infrastructure.Persistence;

/// <summary>Lets <c>dotnet ef</c> create the context at design time (migrations).</summary>
internal sealed class TripsDbContextFactory : IDesignTimeDbContextFactory<TripsDbContext>
{
    public TripsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TripsDbContext>();
        options.UseTripsNpgsql("Host=localhost;Database=trailhub_trips;Username=trailhub;Password=trailhub");
        return new TripsDbContext(options.Options);
    }
}
