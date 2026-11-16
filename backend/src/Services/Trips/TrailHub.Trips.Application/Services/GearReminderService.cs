using TrailHub.BuildingBlocks.Application;
using TrailHub.Contracts;
using TrailHub.Trips.Application.Interfaces.Providers;
using TrailHub.Trips.Application.Interfaces.Repository;
using TrailHub.Trips.Application.Interfaces.Service;
using TrailHub.Trips.Domain.Trips;

namespace TrailHub.Trips.Application.Services;

internal sealed class GearReminderService(
    ITripRepository trips,
    IGroupDirectory directory,
    IUnitOfWork unitOfWork,
    IEventPublisher events,
    TimeProvider clock) : IGearReminderService
{
    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var sent = 0;
        foreach (var trip in await trips.GetGearReminderCandidatesAsync(today.AddDays(Trip.GearReminderDaysBefore), cancellationToken))
        {
            if (!trip.NeedsGearReminder(today) || trip.GroupId is not { } groupId || await directory.GetAsync(groupId, cancellationToken) is not { } group)
            {
                continue;
            }

            // Saved before publishing: a reminder that fails to publish is skipped rather than sent twice.
            trip.MarkGearReminderSent(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await events.PublishAsync(
                new TripGearUnclaimedIntegrationEvent(
                    trip.Id,
                    trip.Name,
                    groupId,
                    trip.UnclaimedGroupItemCount,
                    trip.StartDate,
                    group.Members.Select(m => m.UserId).ToList()),
                cancellationToken);
            sent++;
        }

        return sent;
    }
}
