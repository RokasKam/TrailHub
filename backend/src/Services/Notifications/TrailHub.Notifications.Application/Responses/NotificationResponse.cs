using TrailHub.Notifications.Domain.Notifications;

namespace TrailHub.Notifications.Application.Responses;

/// <param name="TargetUrl">The app path to open, e.g. <c>/feed/{id}</c>, <c>/groups/{id}</c>, <c>/trips/{id}/expenses</c>.</param>
public sealed record NotificationResponse(
    Guid Id,
    NotificationCategory Category,
    NotificationKind Kind,
    string Title,
    string Body,
    string TargetUrl,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool Read);
