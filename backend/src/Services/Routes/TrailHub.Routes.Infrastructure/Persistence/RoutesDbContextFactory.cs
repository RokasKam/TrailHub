using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrailHub.Routes.Infrastructure.Persistence;

internal sealed class RoutesDbContextFactory : IDesignTimeDbContextFactory<RoutesDbContext>
{
    public RoutesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RoutesDbContext>();
        options.UseRoutesNpgsql("Host=localhost;Database=trailhub_routes;Username=trailhub;Password=trailhub");
        return new RoutesDbContext(options.Options);
    }
}
