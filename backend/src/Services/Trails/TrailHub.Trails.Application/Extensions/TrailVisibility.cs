using TrailHub.Trails.Application.Interfaces.Providers;
using TrailHub.Trails.Domain.Trails;

namespace TrailHub.Trails.Application.Extensions;

internal static class TrailVisibility
{
    /// <summary>Hidden trails are visible only to their author and moderators; to everyone else they don't exist.</summary>
    public static bool CanSee(this Trail trail, string userId, IModerators moderators) =>
        !trail.IsHidden || trail.CreatedBy == userId || moderators.IsModerator(userId);
}
