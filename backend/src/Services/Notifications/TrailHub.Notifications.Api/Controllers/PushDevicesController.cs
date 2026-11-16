using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Requests;

namespace TrailHub.Notifications.Api.Controllers;

/// <summary>Phones that receive the caller's pushes, by Expo push token.</summary>
[Route("api/notifications/devices")]
public sealed class PushDevicesController(IPushDeviceService deviceService) : ApiController
{
    /// <summary>Registers (or refreshes) a device for the caller. A token registered by someone else moves to the caller.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterDeviceRequest body, CancellationToken ct)
    {
        await deviceService.RegisterAsync(body, ct);
        return NoContent();
    }

    /// <summary>Stops pushes to a device (on sign-out). Unknown tokens are ignored.</summary>
    [HttpDelete("{token}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unregister(string token, CancellationToken ct)
    {
        await deviceService.UnregisterAsync(token, ct);
        return NoContent();
    }
}
