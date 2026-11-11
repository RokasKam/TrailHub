namespace TrailHub.Routes.Infrastructure;

// Namespace kept as it was: the migrations' model snapshot names this entity by its full CLR name.
internal sealed class KnownGroupMember
{
    public string UserId { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
}
