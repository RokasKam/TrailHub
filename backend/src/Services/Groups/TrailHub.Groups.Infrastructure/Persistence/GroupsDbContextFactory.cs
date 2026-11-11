using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TrailHub.Groups.Infrastructure.Persistence;

internal sealed class GroupsDbContextFactory : IDesignTimeDbContextFactory<GroupsDbContext>
{
    public GroupsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GroupsDbContext>();
        options.UseGroupsNpgsql("Host=localhost;Database=trailhub_groups;Username=trailhub;Password=trailhub");
        return new GroupsDbContext(options.Options);
    }
}
