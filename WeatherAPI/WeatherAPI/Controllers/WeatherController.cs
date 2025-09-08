using Microsoft.AspNetCore.Mvc;
using WeatherAPI.Models;
using WeatherAPI.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace WeatherAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherController> _logger;
    private readonly WeatherTelemetryService _telemetryService;

    public WeatherController(
        IWeatherService weatherService, 
        ILogger<WeatherController> logger,
        WeatherTelemetryService telemetryService)
    {
        _weatherService = weatherService;
        _logger = logger;
        _telemetryService = telemetryService;
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
        var stopwatch = Stopwatch.StartNew();
        var clientIp = GetClientIpAddress();
        var cityState = state != null ? $"{city}, {state}" : city;
        
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
            stopwatch.Stop();
            
            if (response.CurrentWeather == null && response.Forecast == null && response.Alerts == null)
            {
                _logger.LogWarning("No weather data available for city: {City}, state: {State}", city, state);
                
                // Track failed operation
                _telemetryService.TrackWeatherOperation("GetWeather", cityState, false, stopwatch.ElapsedMilliseconds, "No data available");
                
                return Problem(
                    title: "Weather data unavailable",
                    detail: $"Unable to retrieve weather data for {city}{(state != null ? $", {state}" : "")}",
                    statusCode: 503);
            }

            // Track successful operation
            var parameters = new Dictionary<string, object>
            {
                ["days"] = days,
                ["hasState"] = state != null,
                ["dataAvailable"] = new
                {
                    CurrentWeather = response.CurrentWeather != null,
                    Forecast = response.Forecast?.Length ?? 0,
                    Alerts = response.Alerts?.Length ?? 0
                }
            };
            
            _telemetryService.TrackWeatherRequest("GetWeather", cityState, clientIp, parameters, true, stopwatch.ElapsedMilliseconds, 200);

            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing weather request for city: {City}", city);
            
            // Track failed operation
            _telemetryService.TrackWeatherOperation("GetWeather", cityState, false, stopwatch.ElapsedMilliseconds, ex.Message);
            
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
        var stopwatch = Stopwatch.StartNew();
        var clientIp = GetClientIpAddress();
        var cityState = request.State != null ? $"{request.City}, {request.State}" : request.City;
        
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Received POST weather request for city: {City}, state: {State}, days: {Days}", 
                request.City, request.State, request.Days);

            var response = await _weatherService.GetWeatherAsync(request);
            stopwatch.Stop();
            
            if (response.CurrentWeather == null && response.Forecast == null && response.Alerts == null)
            {
                _logger.LogWarning("No weather data available for city: {City}, state: {State}", 
                    request.City, request.State);
                    
                _telemetryService.TrackWeatherOperation("GetWeatherPost", cityState, false, stopwatch.ElapsedMilliseconds, "No data available");
                
                return Problem(
                    title: "Weather data unavailable",
                    detail: $"Unable to retrieve weather data for {request.City}{(request.State != null ? $", {request.State}" : "")}",
                    statusCode: 503);
            }

            // Track successful operation
            var parameters = new Dictionary<string, object>
            {
                ["days"] = request.Days,
                ["hasState"] = request.State != null,
                ["dataAvailable"] = new
                {
                    CurrentWeather = response.CurrentWeather != null,
                    Forecast = response.Forecast?.Length ?? 0,
                    Alerts = response.Alerts?.Length ?? 0
                }
            };
            
            _telemetryService.TrackWeatherRequest("GetWeatherPost", cityState, clientIp, parameters, true, stopwatch.ElapsedMilliseconds, 200);

            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing POST weather request for city: {City}", request.City);
            
            _telemetryService.TrackWeatherOperation("GetWeatherPost", cityState, false, stopwatch.ElapsedMilliseconds, ex.Message);
            
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
        var stopwatch = Stopwatch.StartNew();
        var clientIp = GetClientIpAddress();
        var cityState = state != null ? $"{city}, {state}" : city;
        
        try
        {
            var request = new WeatherRequest { City = city, State = state, Days = 1 };
            var response = await _weatherService.GetWeatherAsync(request);
            stopwatch.Stop();

            if (response.CurrentWeather == null)
            {
                _telemetryService.TrackWeatherOperation("GetCurrentWeather", cityState, false, stopwatch.ElapsedMilliseconds, "Current weather unavailable");
                
                return Problem(
                    title: "Current weather unavailable",
                    detail: $"Unable to retrieve current weather for {city}{(state != null ? $", {state}" : "")}",
                    statusCode: 503);
            }

            // Track successful operation
            _telemetryService.TrackWeatherRequest("GetCurrentWeather", cityState, clientIp, null, true, stopwatch.ElapsedMilliseconds, 200);

            return Ok(response.CurrentWeather);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error getting current weather for city: {City}", city);
            
            _telemetryService.TrackWeatherOperation("GetCurrentWeather", cityState, false, stopwatch.ElapsedMilliseconds, ex.Message);
            
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
        var stopwatch = Stopwatch.StartNew();
        var clientIp = GetClientIpAddress();
        var cityState = state != null ? $"{city}, {state}" : city;
        
        try
        {
            var request = new WeatherRequest { City = city, State = state, Days = days };
            var response = await _weatherService.GetWeatherAsync(request);
            stopwatch.Stop();

            if (response.Forecast == null)
            {
                _telemetryService.TrackWeatherOperation("GetWeatherForecast", cityState, false, stopwatch.ElapsedMilliseconds, "Weather forecast unavailable");
                
                return Problem(
                    title: "Weather forecast unavailable",
                    detail: $"Unable to retrieve weather forecast for {city}{(state != null ? $", {state}" : "")}",
                    statusCode: 503);
            }

            // Track successful operation
            var parameters = new Dictionary<string, object>
            {
                ["days"] = days,
                ["forecastCount"] = response.Forecast.Length
            };
            
            _telemetryService.TrackWeatherRequest("GetWeatherForecast", cityState, clientIp, parameters, true, stopwatch.ElapsedMilliseconds, 200);

            return Ok(response.Forecast);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error getting weather forecast for city: {City}", city);
            
            _telemetryService.TrackWeatherOperation("GetWeatherForecast", cityState, false, stopwatch.ElapsedMilliseconds, ex.Message);
            
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
        var stopwatch = Stopwatch.StartNew();
        var clientIp = GetClientIpAddress();
        var cityState = state != null ? $"{city}, {state}" : city;
        
        try
        {
            var request = new WeatherRequest { City = city, State = state };
            var response = await _weatherService.GetWeatherAsync(request);
            stopwatch.Stop();

            var alerts = response.Alerts ?? Array.Empty<WeatherAlertInfo>();
            
            // Track successful operation
            var parameters = new Dictionary<string, object>
            {
                ["alertCount"] = alerts.Length
            };
            
            _telemetryService.TrackWeatherRequest("GetWeatherAlerts", cityState, clientIp, parameters, true, stopwatch.ElapsedMilliseconds, 200);

            return Ok(alerts);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error getting weather alerts for city: {City}", city);
            
            _telemetryService.TrackWeatherOperation("GetWeatherAlerts", cityState, false, stopwatch.ElapsedMilliseconds, ex.Message);
            
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

    private string? GetClientIpAddress()
    {
        // Check for forwarded IP addresses first (common in load balancers)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to remote IP address
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}