using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Notifications.Application.Interfaces.Service;
using TrailHub.Notifications.Application.Responses;

namespace TrailHub.Notifications.Api.Controllers;

/// <summary>The caller's notification inbox.</summary>
/// <remarks>
/// Every notification is stored here, whether or not it was also pushed (the category switches only control pushes).
/// Likes on one hike share one item that updates and becomes unread again. <c>targetUrl</c> is the app path to open.
/// </remarks>
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ApiController
{
    /// <summary>The caller's notifications, most recently updated first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await notificationService.GetPageAsync(new PageRequest(page ?? 1, pageSize ?? 20), ct));

    /// <summary>How many notifications are unread (the badge).</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken ct) =>
        Ok(await notificationService.GetUnreadCountAsync(ct));

    /// <summary>Marks one notification read.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notificationService.MarkReadAsync(id, ct);
        return NoContent();
    }

    /// <summary>Marks every notification read.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notificationService.MarkAllReadAsync(ct);
        return NoContent();
    }
}
