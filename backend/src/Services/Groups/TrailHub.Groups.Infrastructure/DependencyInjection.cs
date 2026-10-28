using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Infrastructure.Messaging;
using TrailHub.BuildingBlocks.Infrastructure.Persistence;
using TrailHub.Groups.Application.Groups;
using TrailHub.Groups.Domain.Groups;

namespace TrailHub.Groups.Infrastructure;

public sealed class GroupsDbContext(DbContextOptions<GroupsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<HikingGroup> Groups => Set<HikingGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("groups");

        var group = modelBuilder.Entity<HikingGroup>();
        group.HasKey(g => g.Id);
        group.Property(g => g.Name).HasMaxLength(HikingGroup.MaxNameLength).IsRequired();
        group.Property(g => g.Description).HasMaxLength(HikingGroup.MaxTextLength);
        group.Property(g => g.InviteCode).HasMaxLength(HikingGroup.InviteCodeLength).IsRequired();
        group.HasIndex(g => g.InviteCode).IsUnique();
        group.Ignore(g => g.OwnerId);
        group.Property<uint>("xmin").IsRowVersion();

        group.OwnsMany(g => g.Members, member =>
        {
            member.ToTable("GroupMembers");
            member.WithOwner().HasForeignKey("GroupId");
            member.HasKey("GroupId", nameof(GroupMember.UserId));
            member.Property(m => m.UserId).HasMaxLength(128);
            member.Property(m => m.DisplayName).HasMaxLength(80).IsRequired();
            member.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            member.HasIndex(m => m.UserId);
        });

        group.OwnsMany(g => g.Hikes, hike =>
        {
            hike.ToTable("GroupHikes");
            hike.WithOwner().HasForeignKey("GroupId");
            hike.HasKey(h => h.Id);
            hike.Property(h => h.Id).ValueGeneratedNever();
            hike.Property(h => h.Title).HasMaxLength(HikingGroup.MaxNameLength).IsRequired();
            hike.Property(h => h.Notes).HasMaxLength(HikingGroup.MaxTextLength);
            hike.Property(h => h.CreatedBy).HasMaxLength(128).IsRequired();
            hike.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
            hike.OwnsMany(h => h.Rsvps, rsvp =>
            {
                rsvp.ToTable("GroupHikeRsvps");
                rsvp.WithOwner().HasForeignKey("HikeId");
                rsvp.HasKey("HikeId", nameof(HikeRsvp.UserId));
                rsvp.Property(r => r.UserId).HasMaxLength(128);
                rsvp.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            });
            hike.OwnsOne(h => h.MeetingPoint, p =>
            {
                p.Property(x => x.Latitude).HasColumnName("MeetingLatitude");
                p.Property(x => x.Longitude).HasColumnName("MeetingLongitude");
                p.Property(x => x.Elevation).HasColumnName("MeetingElevation");
            });
        });
    }
}

internal sealed class GroupRepository(GroupsDbContext db) : IGroupRepository
{
    public Task<HikingGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Groups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<HikingGroup?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) =>
        db.Groups.FirstOrDefaultAsync(g => g.InviteCode == inviteCode, cancellationToken);

    public async Task<IReadOnlyList<HikingGroup>> GetForMemberAsync(string userId, CancellationToken cancellationToken) =>
        await db.Groups.AsNoTracking().Where(g => g.Members.Any(m => m.UserId == userId)).OrderBy(g => g.Name).ToListAsync(cancellationToken);

    public Task<bool> IsMemberAsync(Guid groupId, string userId, CancellationToken cancellationToken) =>
        db.Groups.AnyAsync(g => g.Id == groupId && g.Members.Any(m => m.UserId == userId), cancellationToken);

    public async Task<IReadOnlyList<HikingGroup>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Groups.AsNoTracking().ToListAsync(cancellationToken);

    public void Add(HikingGroup group) => db.Groups.Add(group);

    public void Remove(HikingGroup group) => db.Groups.Remove(group);
}

/// <summary>Publishes every group's members once the service is up (see <see cref="RepublishGroupMembersCommand"/>).</summary>
internal sealed class GroupMembersResync(IServiceScopeFactory scopes, ILogger<GroupMembersResync> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICommandHandler<RepublishGroupMembersCommand>>().Handle(new RepublishGroupMembersCommand(), stoppingToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Couldn't republish group members; other services may be out of date until the next change");
        }
    }
}

/// <summary>One Redis key per (group, member) with a sliding TTL; falls back to in-memory when Redis isn't configured.</summary>
internal sealed class DistributedLiveLocationStore(IDistributedCache cache) : ILiveLocationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task SetAsync(Guid groupId, LiveLocationDto location, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            Key(groupId, location.UserId),
            JsonSerializer.Serialize(location, Json),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ILiveLocationStore.TimeToLive },
            cancellationToken);

    public async Task<IReadOnlyList<LiveLocationDto>> GetAsync(Guid groupId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var results = new List<LiveLocationDto>(userIds.Count);
        foreach (var userId in userIds)
        {
            if (await cache.GetStringAsync(Key(groupId, userId), cancellationToken) is { } json &&
                JsonSerializer.Deserialize<LiveLocationDto>(json, Json) is { } location)
            {
                results.Add(location);
            }
        }

        return results;
    }

    public Task RemoveAsync(Guid groupId, string userId, CancellationToken cancellationToken) =>
        cache.RemoveAsync(Key(groupId, userId), cancellationToken);

    private static string Key(Guid groupId, string userId) => $"live-location:{groupId:N}:{userId}";
}

public static class DependencyInjection
{
    public static IServiceCollection AddGroupsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GroupsDb")
            ?? throw new InvalidOperationException("Connection string 'GroupsDb' is not configured.");

        services.AddDbContext<GroupsDbContext>(o => o.UseGroupsNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<GroupsDbContext>());
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddSingleton<ILiveLocationStore, DistributedLiveLocationStore>();

        if (configuration.GetConnectionString("Redis") is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = "trailhub:groups:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddTrailHubMessaging(configuration);
        services.AddHostedService<GroupMembersResync>();
        services.AddHealthChecks().AddDbContextCheck<GroupsDbContext>("groups-db", tags: ["ready"]);
        return services;
    }

    public static Task MigrateGroupsDatabaseAsync(this IServiceProvider services) =>
        services.MigrateDatabaseAsync<GroupsDbContext>();

    internal static DbContextOptionsBuilder UseGroupsNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "groups"));
}

internal sealed class GroupsDbContextFactory : IDesignTimeDbContextFactory<GroupsDbContext>
{
    public GroupsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GroupsDbContext>();
        options.UseGroupsNpgsql("Host=localhost;Database=trailhub_groups;Username=trailhub;Password=trailhub");
        return new GroupsDbContext(options.Options);
    }
}
