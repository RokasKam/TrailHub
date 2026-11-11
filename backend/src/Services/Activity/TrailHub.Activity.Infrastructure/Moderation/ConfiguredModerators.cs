using TrailHub.Activity.Application.Interfaces.Providers;

namespace TrailHub.Activity.Infrastructure.Moderation;

/// <summary>Moderators are listed in configuration (<c>Moderation:ModeratorUserIds</c>, Auth0 user ids), as in Trails.</summary>
internal sealed class ConfiguredModerators(IReadOnlyCollection<string> userIds) : IModerators
{
    private readonly HashSet<string> _ids = [.. userIds.Where(id => !string.IsNullOrWhiteSpace(id))];

    public bool IsModerator(string userId) => _ids.Contains(userId);
}
