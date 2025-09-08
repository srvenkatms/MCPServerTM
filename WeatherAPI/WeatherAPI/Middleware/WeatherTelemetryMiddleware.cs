using WeatherAPI.Services;
using System.Diagnostics;

namespace WeatherAPI.Middleware;

/// <summary>
/// Middleware for tracking Weather API requests and performance metrics
/// </summary>
public class WeatherTelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly WeatherTelemetryService _telemetryService;
    private readonly ILogger<WeatherTelemetryMiddleware> _logger;

    public WeatherTelemetryMiddleware(
        RequestDelegate next,
        WeatherTelemetryService telemetryService,
        ILogger<WeatherTelemetryMiddleware> logger)
    {
        _next = next;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var originalResponseBodyStream = context.Response.Body;
        
        try
        {
            // Get client information
            var clientIp = GetClientIpAddress(context);
            var userAgent = context.Request.Headers.UserAgent.ToString();
            
            // Set context items for telemetry service
            context.Items["ClientIP"] = clientIp;
            context.Items["UserAgent"] = userAgent;

            await _next(context);

            stopwatch.Stop();

            // Track the API request
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            _telemetryService.TrackApiRequest(
                endpoint, 
                clientIp,
                context.Response.StatusCode, 
                stopwatch.ElapsedMilliseconds,
                userAgent);

            // Track specific Weather API endpoints with more detail
            if (context.Request.Path.StartsWithSegments("/api/weather"))
            {
                TrackWeatherEndpoint(context, stopwatch.ElapsedMilliseconds, clientIp);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Track the error
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            var clientIp = GetClientIpAddress(context);
            _telemetryService.TrackApiRequest(
                endpoint, 
                clientIp,
                500, 
                stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex, "Unhandled exception in request to {Endpoint}", endpoint);
            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBodyStream;
        }
    }

    private void TrackWeatherEndpoint(HttpContext context, double duration, string? clientIp)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        var method = context.Request.Method;
        var statusCode = context.Response.StatusCode;
        var isSuccess = statusCode >= 200 && statusCode < 400;

        // Extract city from path if available
        var city = ExtractCityFromPath(path);

        switch (path)
        {
            case var currentPath when currentPath?.Contains("/current") == true:
                _telemetryService.TrackWeatherRequest("CurrentWeather", city ?? "Unknown", clientIp, 
                    null, isSuccess, duration, statusCode);
                _telemetryService.TrackCustomMetric("Weather.Current.Requests", 1, new Dictionary<string, string>
                {
                    ["City"] = city ?? "Unknown",
                    ["ClientIP"] = clientIp ?? "Unknown",
                    ["Duration"] = duration.ToString(),
                    ["StatusCode"] = statusCode.ToString()
                });
                break;

            case var forecastPath when forecastPath?.Contains("/forecast") == true:
                _telemetryService.TrackWeatherRequest("ForecastWeather", city ?? "Unknown", clientIp, 
                    null, isSuccess, duration, statusCode);
                _telemetryService.TrackCustomMetric("Weather.Forecast.Requests", 1, new Dictionary<string, string>
                {
                    ["City"] = city ?? "Unknown",
                    ["ClientIP"] = clientIp ?? "Unknown",
                    ["Duration"] = duration.ToString(),
                    ["StatusCode"] = statusCode.ToString()
                });
                break;

            case var alertsPath when alertsPath?.Contains("/alerts") == true:
                _telemetryService.TrackWeatherRequest("WeatherAlerts", city ?? "Unknown", clientIp, 
                    null, isSuccess, duration, statusCode);
                _telemetryService.TrackCustomMetric("Weather.Alerts.Requests", 1, new Dictionary<string, string>
                {
                    ["City"] = city ?? "Unknown",
                    ["ClientIP"] = clientIp ?? "Unknown",
                    ["Duration"] = duration.ToString(),
                    ["StatusCode"] = statusCode.ToString()
                });
                break;

            case var generalPath when generalPath?.StartsWith("/api/weather/") == true && !generalPath.Contains("/current") && !generalPath.Contains("/forecast") && !generalPath.Contains("/alerts"):
                _telemetryService.TrackWeatherRequest("GeneralWeather", city ?? "Unknown", clientIp, 
                    null, isSuccess, duration, statusCode);
                _telemetryService.TrackCustomMetric("Weather.General.Requests", 1, new Dictionary<string, string>
                {
                    ["City"] = city ?? "Unknown",
                    ["ClientIP"] = clientIp ?? "Unknown",
                    ["Duration"] = duration.ToString(),
                    ["StatusCode"] = statusCode.ToString()
                });
                break;
        }

        // Track operation type for overall weather operations
        if (!string.IsNullOrEmpty(city))
        {
            var operationType = ExtractOperationType(path);
            if (!string.IsNullOrEmpty(operationType))
            {
                _telemetryService.TrackWeatherOperation(operationType, city, isSuccess, duration, 
                    isSuccess ? null : $"HTTP {statusCode}");
            }
        }
    }

    private string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP addresses first (common in load balancers)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fall back to remote IP address
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private string? ExtractCityFromPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Expected format: /api/weather/{city} or /api/weather/{city}/current|forecast|alerts
        if (segments.Length >= 3 && segments[0] == "api" && segments[1] == "weather")
        {
            return segments[2];
        }

        return null;
    }

    private string? ExtractOperationType(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (path.Contains("/current"))
            return "GetCurrentWeather";
        else if (path.Contains("/forecast"))
            return "GetForecast";
        else if (path.Contains("/alerts"))
            return "GetAlerts";
        else
            return "GetWeather";
    }
}