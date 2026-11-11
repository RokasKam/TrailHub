namespace TrailHub.Weather.Domain.Forecasts;

public sealed record HikingAssessment(HikingSuitability Suitability, IReadOnlyList<string> Warnings);
