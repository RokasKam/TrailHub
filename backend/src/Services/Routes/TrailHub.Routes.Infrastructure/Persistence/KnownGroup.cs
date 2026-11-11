namespace TrailHub.Routes.Infrastructure;

// Namespace kept as it was: the migrations' model snapshot names this entity by its full CLR name.

/// <summary>A group as last reported by the Groups service. Deleted groups stay as a tombstone so late, older snapshots are ignored.</summary>
internal sealed class KnownGroup
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public bool Deleted { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<KnownGroupMember> Members { get; set; } = [];
}
