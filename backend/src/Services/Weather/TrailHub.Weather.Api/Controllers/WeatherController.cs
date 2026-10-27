using Microsoft.AspNetCore.Mvc;
using TrailHub.BuildingBlocks.Application;
using TrailHub.BuildingBlocks.Web;
using TrailHub.Weather.Application.Forecasts;

namespace TrailHub.Weather.Api.Controllers;

/// <summary>Mountain weather from Open-Meteo, with a per-day hiking suitability assessment.</summary>
[Route("api/weather")]
public sealed class WeatherController : ApiController
{
    /// <summary>Current conditions and up to 16 days of forecast for a location.</summary>
    /// <param name="lat">Latitude.</param>
    /// <param name="lon">Longitude.</param>
    /// <param name="elevation">Trail or summit elevation in meters; the forecast is downscaled to it.</param>
    /// <param name="from">First day to include (yyyy-MM-dd, location's time zone), e.g. a trip's start.</param>
    /// <param name="to">Last day to include.</param>
    /// <remarks>Responses are cached for 20 minutes per ~1 km grid cell.</remarks>
    [HttpGet]
    [ProducesResponseType<ForecastDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ForecastDto>> Get(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] double? elevation,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromServices] IQueryHandler<GetForecastQuery, ForecastDto> handler,
        CancellationToken ct) =>
        FromResult(await handler.Handle(new GetForecastQuery(lat, lon, elevation, from, to), ct));
}
