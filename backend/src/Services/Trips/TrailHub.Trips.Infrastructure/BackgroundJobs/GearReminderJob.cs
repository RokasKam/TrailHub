using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrailHub.Trips.Application.Interfaces.Service;

namespace TrailHub.Trips.Infrastructure.BackgroundJobs;

/// <summary>Runs <see cref="IGearReminderService.SendDueRemindersAsync"/> on start and then every hour.</summary>
internal sealed class GearReminderJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<GearReminderJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sent = await scope.ServiceProvider.GetRequiredService<IGearReminderService>().SendDueRemindersAsync(stoppingToken);
                if (sent > 0)
                {
                    logger.LogInformation("Sent {Count} gear reminder(s)", sent);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Gear reminders failed; retrying in an hour");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
