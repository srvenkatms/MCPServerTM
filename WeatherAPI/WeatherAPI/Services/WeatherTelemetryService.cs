using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Collections.Concurrent;

namespace WeatherAPI.Services;

/// <summary>
/// Service for tracking Weather API-specific telemetry and anomaly detection
/// </summary>
public class WeatherTelemetryService
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeatherTelemetryService> _logger;
    
    // In-memory counters for anomaly detection
    private readonly ConcurrentDictionary<string, List<DateTime>> _ipRequestCounts = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _ipFailureCounts = new();
    
    // Configuration values
    private readonly int _maxRequestsPerMinute;
    private readonly int _maxFailuresPerMinute;
    private readonly bool _isTelemetryEnabled;

    public WeatherTelemetryService(
        IConfiguration configuration,
        ILogger<WeatherTelemetryService> logger,
        TelemetryClient? telemetryClient = null)
    {
        _telemetryClient = telemetryClient;
        _configuration = configuration;
        _logger = logger;
        _isTelemetryEnabled = _telemetryClient != null;
        
        // Load anomaly detection thresholds
        _maxRequestsPerMinute = configuration.GetValue<int>("ApplicationInsights:CustomTelemetry:AnomalyDetection:MaxRequestsPerMinute", 100);
        _maxFailuresPerMinute = configuration.GetValue<int>("ApplicationInsights:CustomTelemetry:AnomalyDetection:MaxFailuresPerMinute", 10);
        
        if (!_isTelemetryEnabled)
        {
            _logger.LogWarning("Application Insights TelemetryClient not available. Telemetry tracking will be disabled.");
        }
    }

    /// <summary>
    /// Track weather request with detailed parameters
    /// </summary>
    public void TrackWeatherRequest(string requestType, string city, string? clientIp = null, Dictionary<string, object>? parameters = null, bool isSuccess = true, double? duration = null, int? statusCode = null)
    {
        if (!_isTelemetryEnabled || !_configuration.GetValue<bool>("ApplicationInsights:CustomTelemetry:TrackWeatherRequests", true))
        {
            // Log locally when telemetry is disabled
            _logger.LogInformation("Weather request: {RequestType} for {City}, Success: {IsSuccess}, Duration: {Duration}ms", requestType, city, isSuccess, duration);
            return;
        }

        var telemetryProperties = new Dictionary<string, string>
        {
            ["RequestType"] = requestType,
            ["City"] = city,
            ["IsSuccess"] = isSuccess.ToString(),
            ["ClientIP"] = clientIp ?? "Unknown"
        };

        var telemetryMetrics = new Dictionary<string, double>();
        
        if (duration.HasValue)
            telemetryMetrics["Duration"] = duration.Value;

        if (statusCode.HasValue)
            telemetryProperties["StatusCode"] = statusCode.Value.ToString();

        // Add parameter information (sanitized)
        if (parameters != null)
        {
            foreach (var param in parameters.Take(10)) // Limit to avoid too much data
            {
                if (param.Value != null)
                {
                    var sanitizedKey = $"Parameter_{SanitizeKey(param.Key)}";
                    telemetryProperties[sanitizedKey] = param.Value.ToString() ?? "null";
                }
            }
        }

        _telemetryClient?.TrackEvent("Weather_Request", telemetryProperties, telemetryMetrics);

        // Check for anomalies based on client IP
        if (!string.IsNullOrEmpty(clientIp))
        {
            CheckForAnomalies(clientIp, isSuccess);
        }
    }

    /// <summary>
    /// Track specific weather operation types
    /// </summary>
    public void TrackWeatherOperation(string operationType, string city, bool isSuccess, double? duration = null, string? errorMessage = null)
    {
        if (!_isTelemetryEnabled)
        {
            _logger.LogInformation("Weather operation: {OperationType} for {City}, Success: {IsSuccess}", operationType, city, isSuccess);
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["OperationType"] = operationType,
            ["City"] = city,
            ["IsSuccess"] = isSuccess.ToString()
        };

        var metrics = new Dictionary<string, double>();
        
        if (duration.HasValue)
            metrics["Duration"] = duration.Value;

        if (!string.IsNullOrEmpty(errorMessage))
        {
            properties["ErrorMessage"] = errorMessage;
        }

        _telemetryClient?.TrackEvent("Weather_Operation", properties, metrics);
    }

    /// <summary>
    /// Track API request patterns
    /// </summary>
    public void TrackApiRequest(string endpoint, string? clientIp, int statusCode, double duration, string? userAgent = null)
    {
        if (!_isTelemetryEnabled)
        {
            // Log locally when telemetry is disabled
            _logger.LogInformation("API request: {Endpoint}, StatusCode: {StatusCode}, Duration: {Duration}ms", endpoint, statusCode, duration);
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["Endpoint"] = endpoint,
            ["StatusCode"] = statusCode.ToString(),
            ["ClientIP"] = clientIp ?? "Unknown"
        };

        if (!string.IsNullOrEmpty(userAgent))
        {
            properties["UserAgent"] = userAgent;
        }

        var metrics = new Dictionary<string, double>
        {
            ["Duration"] = duration
        };

        _telemetryClient?.TrackEvent("Weather_ApiRequest", properties, metrics);
    }

    /// <summary>
    /// Track detected anomalies
    /// </summary>
    public void TrackAnomaly(string anomalyType, string clientIdentifier, Dictionary<string, string>? additionalProperties = null)
    {
        if (!_isTelemetryEnabled || !_configuration.GetValue<bool>("ApplicationInsights:CustomTelemetry:TrackAnomalies", true))
        {
            // Always log anomalies locally, even when telemetry is disabled
            _logger.LogWarning("Anomaly detected: {AnomalyType} for client {ClientIdentifier}", anomalyType, clientIdentifier);
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["AnomalyType"] = anomalyType,
            ["ClientIdentifier"] = clientIdentifier,
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        if (additionalProperties != null)
        {
            foreach (var prop in additionalProperties)
            {
                properties[$"Additional_{prop.Key}"] = prop.Value;
            }
        }

        _telemetryClient?.TrackEvent("Weather_Anomaly", properties);
        
        // Also log as a warning for immediate attention
        _logger.LogWarning("Anomaly detected: {AnomalyType} for client {ClientIdentifier}", anomalyType, clientIdentifier);
    }

    /// <summary>
    /// Track custom metrics
    /// </summary>
    public void TrackCustomMetric(string metricName, double value, Dictionary<string, string>? properties = null)
    {
        if (!_isTelemetryEnabled)
        {
            // Log locally when telemetry is disabled
            _logger.LogInformation("Custom metric: {MetricName} = {Value}", metricName, value);
            return;
        }

        _telemetryClient?.TrackMetric(metricName, value, properties);
    }

    /// <summary>
    /// Track performance metrics for external dependencies
    /// </summary>
    public void TrackDependency(string dependencyName, string command, bool isSuccess, double duration, string? resultCode = null)
    {
        if (!_isTelemetryEnabled || !_configuration.GetValue<bool>("ApplicationInsights:CustomTelemetry:TrackPerformanceMetrics", true))
        {
            _logger.LogInformation("Dependency call: {DependencyName} - {Command}, Success: {IsSuccess}, Duration: {Duration}ms", dependencyName, command, isSuccess, duration);
            return;
        }

        var startTime = DateTimeOffset.UtcNow.AddMilliseconds(-duration);
        var dependencyTelemetry = new DependencyTelemetry
        {
            Type = dependencyName,
            Name = command,
            Data = command,
            Timestamp = startTime,
            Duration = TimeSpan.FromMilliseconds(duration),
            Success = isSuccess,
            ResultCode = resultCode
        };

        _telemetryClient?.TrackDependency(dependencyTelemetry);
    }

    /// <summary>
    /// Flush telemetry data
    /// </summary>
    public void FlushTelemetry()
    {
        _telemetryClient?.Flush();
    }

    private void CheckForAnomalies(string clientIdentifier, bool isSuccess)
    {
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        // Track request count
        var requestTimes = _ipRequestCounts.GetOrAdd(clientIdentifier, _ => new List<DateTime>());
        lock (requestTimes)
        {
            requestTimes.Add(now);
            // Remove old entries
            requestTimes.RemoveAll(t => t < oneMinuteAgo);
            
            // Check for high request rate
            if (requestTimes.Count > _maxRequestsPerMinute)
            {
                TrackAnomaly("HighRequestRate", clientIdentifier, new Dictionary<string, string>
                {
                    ["RequestCount"] = requestTimes.Count.ToString(),
                    ["ThresholdExceeded"] = _maxRequestsPerMinute.ToString()
                });
            }
        }

        // Track failure count
        if (!isSuccess)
        {
            var failureTimes = _ipFailureCounts.GetOrAdd(clientIdentifier, _ => new List<DateTime>());
            lock (failureTimes)
            {
                failureTimes.Add(now);
                // Remove old entries
                failureTimes.RemoveAll(t => t < oneMinuteAgo);
                
                // Check for high failure rate
                if (failureTimes.Count > _maxFailuresPerMinute)
                {
                    TrackAnomaly("HighFailureRate", clientIdentifier, new Dictionary<string, string>
                    {
                        ["FailureCount"] = failureTimes.Count.ToString(),
                        ["ThresholdExceeded"] = _maxFailuresPerMinute.ToString()
                    });
                }
            }
        }

        // Cleanup old data periodically (simple approach)
        if (now.Second == 0) // Every minute
        {
            CleanupOldData(oneMinuteAgo);
        }
    }

    private void CleanupOldData(DateTime cutoff)
    {
        foreach (var kvp in _ipRequestCounts.ToList())
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => t < cutoff);
                if (kvp.Value.Count == 0)
                {
                    _ipRequestCounts.TryRemove(kvp.Key, out _);
                }
            }
        }

        foreach (var kvp in _ipFailureCounts.ToList())
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => t < cutoff);
                if (kvp.Value.Count == 0)
                {
                    _ipFailureCounts.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    private string SanitizeKey(string key)
    {
        // Remove potentially sensitive or problematic characters
        return System.Text.RegularExpressions.Regex.Replace(key, @"[^\w\-_]", "_");
    }
}