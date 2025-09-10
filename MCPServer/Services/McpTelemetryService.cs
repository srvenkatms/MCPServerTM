using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace MCPServer.Services;

/// <summary>
/// Service for tracking MCP-specific telemetry and anomaly detection
/// </summary>
public class McpTelemetryService
{
    private readonly TelemetryClient? _telemetryClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<McpTelemetryService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    // In-memory counters for anomaly detection
    private readonly ConcurrentDictionary<string, List<DateTime>> _userRequestCounts = new();
    private readonly ConcurrentDictionary<string, List<DateTime>> _userFailureCounts = new();
    
    // Configuration values
    private readonly int _maxRequestsPerMinute;
    private readonly int _maxFailuresPerMinute;
    private readonly bool _isTelemetryEnabled;

    public McpTelemetryService(
        IConfiguration configuration,
        ILogger<McpTelemetryService> logger,
        IHttpContextAccessor httpContextAccessor,
        TelemetryClient? telemetryClient = null)
    {
        _telemetryClient = telemetryClient;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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
    /// Track tool usage with detailed parameters
    /// </summary>
    public void TrackToolUsage(string toolName, ClaimsPrincipal? user, Dictionary<string, object>? parameters = null, bool isSuccess = true, double? duration = null, string? correlationId = null)
    {
        if (!_isTelemetryEnabled || !_configuration.GetValue<bool>("ApplicationInsights:CustomTelemetry:TrackToolUsage", true))
        {
            // Log locally when telemetry is disabled
            _logger.LogInformation("Tool usage: {ToolName}, Success: {IsSuccess}, Duration: {Duration}ms", toolName, isSuccess, duration);
            return;
        }

        var userId = GetUserId(user);
        var telemetryProperties = new Dictionary<string, string>
        {
            ["ToolName"] = toolName,
            ["UserId"] = userId ?? "Anonymous",
            ["IsSuccess"] = isSuccess.ToString(),
            ["UserAgent"] = GetUserAgent(),
            ["ClientId"] = GetClientId(user)
        };

        // Add correlation ID if provided
        if (!string.IsNullOrEmpty(correlationId))
        {
            telemetryProperties["CorrelationId"] = correlationId;
        }

        var telemetryMetrics = new Dictionary<string, double>();
        
        if (duration.HasValue)
            telemetryMetrics["Duration"] = duration.Value;

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

        _telemetryClient?.TrackEvent("MCP_ToolUsage", telemetryProperties, telemetryMetrics);

        // Check for anomalies
        if (userId != null)
        {
            CheckForAnomalies(userId, isSuccess);
        }
    }

    /// <summary>
    /// Track authentication events
    /// </summary>
    public void TrackAuthenticationEvent(string eventType, ClaimsPrincipal? user, bool isSuccess, string? errorMessage = null, string? correlationId = null)
    {
        if (!_isTelemetryEnabled || !_configuration.GetValue<bool>("ApplicationInsights:CustomTelemetry:TrackAuthenticationEvents", true))
        {
            // Log locally when telemetry is disabled
            _logger.LogInformation("Authentication event: {EventType}, Success: {IsSuccess}, Error: {ErrorMessage}", eventType, isSuccess, errorMessage);
            return;
        }

        var userId = GetUserId(user);
        var properties = new Dictionary<string, string>
        {
            ["EventType"] = eventType,
            ["UserId"] = userId ?? "Anonymous",
            ["IsSuccess"] = isSuccess.ToString(),
            ["UserAgent"] = GetUserAgent(),
            ["ClientId"] = GetClientId(user)
        };

        // Add correlation ID if provided
        if (!string.IsNullOrEmpty(correlationId))
        {
            properties["CorrelationId"] = correlationId;
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            properties["ErrorMessage"] = errorMessage;
        }

        // Add scope information if available
        var scopes = GetUserScopes(user);
        if (scopes.Any())
        {
            properties["Scopes"] = string.Join(",", scopes);
        }

        _telemetryClient?.TrackEvent("MCP_Authentication", properties);

        // Track failed authentication attempts for anomaly detection
        if (!isSuccess && userId != null)
        {
            CheckForAnomalies(userId, false);
        }
    }

    /// <summary>
    /// Track API request patterns
    /// </summary>
    public void TrackApiRequest(string endpoint, ClaimsPrincipal? user, int statusCode, double duration, string? correlationId = null)
    {
        if (!_isTelemetryEnabled)
        {
            // Log locally when telemetry is disabled
            _logger.LogInformation("API request: {Endpoint}, StatusCode: {StatusCode}, Duration: {Duration}ms", endpoint, statusCode, duration);
            return;
        }

        var userId = GetUserId(user);
        var properties = new Dictionary<string, string>
        {
            ["Endpoint"] = endpoint,
            ["UserId"] = userId ?? "Anonymous",
            ["StatusCode"] = statusCode.ToString(),
            ["UserAgent"] = GetUserAgent(),
            ["ClientId"] = GetClientId(user)
        };

        // Add correlation ID if provided
        if (!string.IsNullOrEmpty(correlationId))
        {
            properties["CorrelationId"] = correlationId;
        }

        var metrics = new Dictionary<string, double>
        {
            ["Duration"] = duration
        };

        _telemetryClient?.TrackEvent("MCP_ApiRequest", properties, metrics);
    }

    /// <summary>
    /// Track detected anomalies
    /// </summary>
    public void TrackAnomaly(string anomalyType, string userId, Dictionary<string, string>? additionalProperties = null, string? correlationId = null)
    {
        if (!_isTelemetryEnabled || !_configuration.GetValue<bool>("ApplicationInsights:CustomTelemetry:TrackAnomalies", true))
        {
            // Always log anomalies locally, even when telemetry is disabled
            _logger.LogWarning("Anomaly detected: {AnomalyType} for user {UserId}", anomalyType, userId);
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["AnomalyType"] = anomalyType,
            ["UserId"] = userId,
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        // Add correlation ID if provided
        if (!string.IsNullOrEmpty(correlationId))
        {
            properties["CorrelationId"] = correlationId;
        }

        if (additionalProperties != null)
        {
            foreach (var prop in additionalProperties)
            {
                properties[$"Additional_{prop.Key}"] = prop.Value;
            }
        }

        _telemetryClient?.TrackEvent("MCP_Anomaly", properties);
        
        // Also log as a warning for immediate attention
        _logger.LogWarning("Anomaly detected: {AnomalyType} for user {UserId}", anomalyType, userId);
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
    /// Flush telemetry data
    /// </summary>
    public void FlushTelemetry()
    {
        _telemetryClient?.Flush();
    }

    private void CheckForAnomalies(string userId, bool isSuccess)
    {
        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        // Track request count
        var requestTimes = _userRequestCounts.GetOrAdd(userId, _ => new List<DateTime>());
        lock (requestTimes)
        {
            requestTimes.Add(now);
            // Remove old entries
            requestTimes.RemoveAll(t => t < oneMinuteAgo);
            
            // Check for high request rate
            if (requestTimes.Count > _maxRequestsPerMinute)
            {
                TrackAnomaly("HighRequestRate", userId, new Dictionary<string, string>
                {
                    ["RequestCount"] = requestTimes.Count.ToString(),
                    ["ThresholdExceeded"] = _maxRequestsPerMinute.ToString()
                });
            }
        }

        // Track failure count
        if (!isSuccess)
        {
            var failureTimes = _userFailureCounts.GetOrAdd(userId, _ => new List<DateTime>());
            lock (failureTimes)
            {
                failureTimes.Add(now);
                // Remove old entries
                failureTimes.RemoveAll(t => t < oneMinuteAgo);
                
                // Check for high failure rate
                if (failureTimes.Count > _maxFailuresPerMinute)
                {
                    TrackAnomaly("HighFailureRate", userId, new Dictionary<string, string>
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
        foreach (var kvp in _userRequestCounts.ToList())
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => t < cutoff);
                if (kvp.Value.Count == 0)
                {
                    _userRequestCounts.TryRemove(kvp.Key, out _);
                }
            }
        }

        foreach (var kvp in _userFailureCounts.ToList())
        {
            lock (kvp.Value)
            {
                kvp.Value.RemoveAll(t => t < cutoff);
                if (kvp.Value.Count == 0)
                {
                    _userFailureCounts.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    private string? GetUserId(ClaimsPrincipal? user)
    {
        return user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
            ?? user?.FindFirst("sub")?.Value 
            ?? user?.FindFirst("oid")?.Value
            ?? user?.Identity?.Name;
    }

    private string GetClientId(ClaimsPrincipal? user)
    {
        return user?.FindFirst("appid")?.Value 
            ?? user?.FindFirst("azp")?.Value 
            ?? "Unknown";
    }

    private IEnumerable<string> GetUserScopes(ClaimsPrincipal? user)
    {
        var scopeClaim = user?.FindFirst("scp")?.Value ?? user?.FindFirst("scope")?.Value;
        if (string.IsNullOrEmpty(scopeClaim))
            return Enumerable.Empty<string>();
            
        return scopeClaim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private string GetUserAgent()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue("UserAgent", out var userAgent) == true)
        {
            var userAgentString = userAgent?.ToString() ?? "Unknown";
            _logger.LogDebug("Retrieved UserAgent from HttpContext: {UserAgent}", userAgentString);
            return userAgentString;
        }
        _logger.LogDebug("UserAgent not found in HttpContext, returning Unknown");
        return "Unknown";
    }

    private string? GetCorrelationIdFromContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue("CorrelationId", out var correlationId) == true)
        {
            return correlationId?.ToString();
        }
        return null;
    }

    private string SanitizeKey(string key)
    {
        // Remove potentially sensitive or problematic characters
        return System.Text.RegularExpressions.Regex.Replace(key, @"[^\w\-_]", "_");
    }
}