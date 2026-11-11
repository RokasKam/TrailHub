namespace TrailHub.Profile.Application.Interfaces.Service;

/// <summary>
/// Keeps profile totals and achievements in step with hikes. Driven by integration events from the
/// Activity service, not by HTTP, so the methods take the user id explicitly instead of reading ICurrentUser.
/// </summary>
public interface IProfileHikeService
{
    /// <summary>Counts a completed hike and returns the codes of any achievements it unlocked. Recording the same activity twice is a no-op.</summary>
    Task<IReadOnlyList<string>> RecordCompletedHikeAsync(
        string userId,
        Guid activityId,
        double distanceMeters,
        double elevationGainMeters,
        DateTime completedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Removes a deleted hike from the user's totals (if the profile exists). Achievements already earned are kept.</summary>
    Task RemoveHikeAsync(string userId, Guid activityId, CancellationToken cancellationToken);
}
