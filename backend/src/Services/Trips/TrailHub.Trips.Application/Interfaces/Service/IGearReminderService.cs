namespace TrailHub.Trips.Application.Interfaces.Service;

/// <summary>Reminds group members about unclaimed group gear shortly before a trip.</summary>
public interface IGearReminderService
{
    /// <summary>
    /// For every planned group trip starting in two days (by UTC date) with unclaimed group items, records the reminder
    /// and publishes <c>TripGearUnclaimedIntegrationEvent</c> once. Returns how many trips were reminded.
    /// </summary>
    Task<int> SendDueRemindersAsync(CancellationToken cancellationToken);
}
