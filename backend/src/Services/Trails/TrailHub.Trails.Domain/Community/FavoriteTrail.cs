using TrailHub.BuildingBlocks.Domain;

namespace TrailHub.Trails.Domain.Community;

public sealed class FavoriteTrail
{
    private FavoriteTrail()
    {
    }

    public string UserId { get; private set; } = null!;

    public Guid TrailId { get; private set; }

    public DateTime SavedAtUtc { get; private set; }

    public static FavoriteTrail Create(string userId, Guid trailId, DateTime nowUtc) =>
        new() { UserId = userId, TrailId = trailId, SavedAtUtc = nowUtc };
}
