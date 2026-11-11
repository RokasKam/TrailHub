using TrailHub.Activity.Application.Interfaces.Repository;
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
}
