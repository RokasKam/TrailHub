namespace TrailHub.Activity.Application.Responses;

public sealed record HikingStatisticsResponse(
    ActivityTotalsResponse AllTime,
    ActivityTotalsResponse ThisYear,
    IReadOnlyList<MonthlyStatisticsResponse> LastTwelveMonths);
