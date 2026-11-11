namespace TrailHub.Weather.Application.Requests;

/// <param name="From">Optional first day to return (e.g. a planned trip's start), in the location's time zone.</param>
/// <param name="To">Optional last day to return.</param>
/// <param name="ElevationMeters">Trail/summit elevation. Forecasts are downscaled to it, which matters a lot in mountains.</param>
public sealed record ForecastRequest(double Latitude, double Longitude, double? ElevationMeters, DateOnly? From, DateOnly? To);
