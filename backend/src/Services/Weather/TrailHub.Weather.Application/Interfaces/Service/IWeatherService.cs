using TrailHub.Weather.Application.Requests;
using TrailHub.Weather.Application.Responses;

namespace TrailHub.Weather.Application.Interfaces.Service;

/// <summary>Mountain weather forecasts with a per-day hiking suitability assessment.</summary>
public interface IWeatherService
{
    /// <summary>
    /// Current conditions and the forecast days within the requested range. Responses are cached for
    /// 20 minutes per ~1 km / 50 m grid cell, so nearby requests share one upstream call.
    /// </summary>
    Task<ForecastResponse> GetForecastAsync(ForecastRequest request, CancellationToken cancellationToken);
}
