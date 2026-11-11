namespace TrailHub.Activity.Application.Responses;

public sealed record MonthlyStatisticsResponse(int Year, int Month, int Hikes, double DistanceMeters, double ElevationGainMeters);
