using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Requests;
using TrailHub.Notifications.Application.Responses;

namespace TrailHub.Notifications.Api.Controllers;

/// <summary>Push on/off per category (all on by default). The inbox records everything regardless.</summary>
[Route("api/notifications/preferences")]
public sealed class NotificationPreferencesController(INotificationPreferencesService preferencesService) : ApiController
{
    [HttpGet]
    [ProducesResponseType<PreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferencesResponse>> Get(CancellationToken ct) =>
        Ok(await preferencesService.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType<PreferencesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferencesResponse>> Update(PreferencesRequest body, CancellationToken ct) =>
        Ok(await preferencesService.UpdateAsync(body, ct));
}
