using TrailHub.Activity.Application.Interfaces.Repository;
using TrailHub.Activity.Application.Models;
using TrailHub.Activity.Domain.Activities;
using TrailHub.BuildingBlocks.Application.Exceptions;

namespace TrailHub.Activity.Application.Services;

internal static class ActivityAccess
{
    /// <summary>The caller's own hike. Other people's hikes are reported as not found.</summary>
    public static async Task<HikeActivity> GetOwnAsync(this IActivityRepository activities, Guid id, string userId, CancellationToken cancellationToken)
    {
        var activity = await activities.GetByIdAsync(id, cancellationToken);
        return activity is not null && activity.UserId == userId
            ? activity
            : throw ActivityErrors.NotFound.ToException();
    }

    /// <summary>A post the caller can see (theirs, or shared with a group they're in now). Anything else is not found.</summary>
    public static async Task<(HikeActivity Activity, IReadOnlyList<GroupInfo> MyGroups)> GetVisibleAsync(
        this IActivityRepository activities,
        IGroupDirectory directory,
        Guid id,
        string userId,
        CancellationToken cancellationToken)
    {
        var myGroups = await directory.GetForMemberAsync(userId, cancellationToken);
        var activity = await activities.GetByIdAsync(id, cancellationToken);
        return activity is not null && activity.IsVisibleTo(userId, myGroups.Select(g => g.Id).ToList())
            ? (activity, myGroups)
            : throw ActivityErrors.NotFound.ToException();
    }
}
