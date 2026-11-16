namespace TrailHub.Notifications.Infrastructure;

// Stays in this namespace: the EF model snapshot names the entity "TrailHub.Notifications.Infrastructure.KnownGroupMember".

internal sealed class KnownGroupMember
{
    public string UserId { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
}
