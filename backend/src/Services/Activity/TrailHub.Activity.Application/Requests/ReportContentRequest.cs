using TrailHub.Activity.Domain.Feed;

namespace TrailHub.Activity.Application.Requests;

/// <param name="Reason">Inappropriate, Spam, Harassment or Other.</param>
/// <param name="Details">Optional, at most 1000 characters.</param>
public sealed record ReportContentRequest(ContentReportReason Reason, string? Details);
