using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrailHub.Groups.Application.Interfaces.Service;

namespace TrailHub.Groups.Infrastructure.BackgroundJobs;

/// <summary>Publishes every group's members once the service is up (see <see cref="IGroupService.RepublishMembersAsync"/>).</summary>
internal sealed class GroupMembersResync(IServiceScopeFactory scopes, ILogger<GroupMembersResync> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IGroupService>().RepublishMembersAsync(stoppingToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Couldn't republish group members; other services may be out of date until the next change");
        }
    }
}
