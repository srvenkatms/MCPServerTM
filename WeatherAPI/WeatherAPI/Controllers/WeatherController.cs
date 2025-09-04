using Microsoft.AspNetCore.Mvc;
using WeatherAPI.Models;
using WeatherAPI.Services;
using System.ComponentModel.DataAnnotations;

namespace WeatherAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(IWeatherService weatherService, ILogger<WeatherController> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }

    /// <summary>
    /// Get weather information for a city
    /// </summary>
    /// <param name="city">The city name (required)</param>
    /// <param name="state">The state code (optional, e.g., 'CA', 'TX')</param>
    /// <param name="days">Number of forecast days (1-7, default: 5)</param>
    /// <returns>Weather information including current conditions, forecast, and alerts</returns>
    [HttpGet("{city}")]
    [ProducesResponseType(typeof(WeatherResponse), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WeatherResponse>> GetWeather(
        [FromRoute] [Required] string city,
        [FromQuery] string? state = null,
        [FromQuery] [Range(1, 7)] int days = 5)
    {
        try
        {
            _logger.LogInformation("Received weather request for city: {City}, state: {State}, days: {Days}", 
                city, state, days);

            var request = new WeatherRequest
            {
                City = city,
                State = state,
                Days = days
            };

            var response = await _weatherService.GetWeatherAsync(request);
            
            if (response.CurrentWeather == null && response.Forecast == null && response.Alerts == null)
            {
                _logger.LogWarning("No weather data available for city: {City}, state: {State}", city, state);
                return Problem(
                    title: "Weather data unavailable",
                    detail: $"Unable to retrieve weather data for {city}{(state != null ? $", {state}" : "")}",
                    statusCode: 503);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing weather request for city: {City}", city);
            return Problem(
                title: "Internal server error",
                detail: "An error occurred while processing your weather request",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Get weather information for a city using POST method
    /// </summary>
    /// <param name="request">Weather request details</param>
    /// <returns>Weather information including current conditions, forecast, and alerts</returns>
    [HttpPost]
    [ProducesResponseType(typeof(WeatherResponse), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WeatherResponse>> GetWeatherPost([FromBody] WeatherRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Received POST weather request for city: {City}, state: {State}, days: {Days}", 
                request.City, request.State, request.Days);

            var response = await _weatherService.GetWeatherAsync(request);
            
            if (response.CurrentWeather == null && response.Forecast == null && response.Alerts == null)
            {
                _logger.LogWarning("No weather data available for city: {City}, state: {State}", 
                    request.City, request.State);
                return Problem(
                    title: "Weather data unavailable",
                    detail: $"Unable to retrieve weather data for {request.City}{(request.State != null ? $", {request.State}" : "")}",
                    statusCode: 503);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing POST weather request for city: {City}", request.City);
            return Problem(
                title: "Internal server error",
                detail: "An error occurred while processing your weather request",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Get current weather only for a city
    /// </summary>
    /// <param name="city">The city name</param>
    /// <param name="state">The state code (optional)</param>
    /// <returns>Current weather information</returns>
    [HttpGet("{city}/current")]
    [ProducesResponseType(typeof(CurrentWeatherInfo), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<CurrentWeatherInfo>> GetCurrentWeather(
        [FromRoute] [Required] string city,
        [FromQuery] string? state = null)
    {
        try
        {
            var request = new WeatherRequest { City = city, State = state, Days = 1 };
            var response = await _weatherService.GetWeatherAsync(request);

            if (response.CurrentWeather == null)
            {
                return Problem(
                    title: "Current weather unavailable",
                    detail: $"Unable to retrieve current weather for {city}{(state != null ? $", {state}" : "")}",
                    statusCode: 503);
            }

            return Ok(response.CurrentWeather);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current weather for city: {City}", city);
            return Problem(
                title: "Internal server error", 
                detail: "An error occurred while processing your current weather request",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Get weather forecast only for a city
    /// </summary>
    /// <param name="city">The city name</param>
    /// <param name="state">The state code (optional)</param>
    /// <param name="days">Number of forecast days (1-7)</param>
    /// <returns>Weather forecast information</returns>
    [HttpGet("{city}/forecast")]
    [ProducesResponseType(typeof(WeatherForecastInfo[]), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WeatherForecastInfo[]>> GetWeatherForecast(
        [FromRoute] [Required] string city,
        [FromQuery] string? state = null,
        [FromQuery] [Range(1, 7)] int days = 5)
    {
        try
        {
            var request = new WeatherRequest { City = city, State = state, Days = days };
            var response = await _weatherService.GetWeatherAsync(request);

            if (response.Forecast == null)
            {
                return Problem(
                    title: "Weather forecast unavailable",
                    detail: $"Unable to retrieve weather forecast for {city}{(state != null ? $", {state}" : "")}",
                    statusCode: 503);
            }

            return Ok(response.Forecast);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather forecast for city: {City}", city);
            return Problem(
                title: "Internal server error",
                detail: "An error occurred while processing your weather forecast request",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Get weather alerts for a city
    /// </summary>
    /// <param name="city">The city name</param>
    /// <param name="state">The state code (optional)</param>
    /// <returns>Weather alerts information</returns>
    [HttpGet("{city}/alerts")]
    [ProducesResponseType(typeof(WeatherAlertInfo[]), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<WeatherAlertInfo[]>> GetWeatherAlerts(
        [FromRoute] [Required] string city,
        [FromQuery] string? state = null)
    {
        try
        {
            var request = new WeatherRequest { City = city, State = state };
            var response = await _weatherService.GetWeatherAsync(request);

            if (response.Alerts == null)
            {
                return Ok(Array.Empty<WeatherAlertInfo>());
            }

            return Ok(response.Alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weather alerts for city: {City}", city);
            return Problem(
                title: "Internal server error",
                detail: "An error occurred while processing your weather alerts request",
                statusCode: 500);
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>API health status</returns>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public ActionResult<object> HealthCheck()
    {
        return Ok(new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            service = "Weather API"
        });
    }
}