using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Application.Exceptions;
using TrailHub.Notifications.Application.Interfaces.Repository;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Requests;
using TrailHub.Notifications.Domain.Devices;

namespace TrailHub.Notifications.Application.Services;

internal sealed class PushDeviceService(
    IPushDeviceRepository devices,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    TimeProvider clock) : IPushDeviceService
{
    public async Task RegisterAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = request.Token?.Trim();
        if (PushDevice.IsValidToken(token) && await devices.GetByTokenAsync(token!, cancellationToken) is { } existing)
        {
            existing.Refresh(user.UserId, request.Platform, now).ThrowIfFailure();
        }
        else
        {
            devices.Add(PushDevice.Register(token, user.UserId, request.Platform, now).GetValueOrThrow());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterAsync(string token, CancellationToken cancellationToken)
    {
        if (await devices.GetByTokenAsync(token.Trim(), cancellationToken) is { } device && device.UserId == user.UserId)
        {
            devices.Remove(device);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
