namespace TrailHub.Trips.Infrastructure;

// Stays in this namespace: the EF model snapshot names the entity "TrailHub.Trips.Infrastructure.KnownGroupMember".

internal sealed class KnownGroupMember
{
    public string UserId { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
}
