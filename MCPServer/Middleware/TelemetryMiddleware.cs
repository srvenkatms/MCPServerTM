using MCPServer.Services;
using System.Diagnostics;

namespace MCPServer.Middleware;

/// <summary>
/// Middleware for tracking API requests and performance metrics
/// </summary>
public class TelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly McpTelemetryService _telemetryService;
    private readonly ILogger<TelemetryMiddleware> _logger;

    public TelemetryMiddleware(
        RequestDelegate next,
        McpTelemetryService telemetryService,
        ILogger<TelemetryMiddleware> logger)
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
            // Set User-Agent in HttpContext items for telemetry service
            var userAgent = context.Request.Headers.UserAgent.ToString();
            context.Items["UserAgent"] = userAgent;

            await _next(context);

            stopwatch.Stop();

            // Track the API request
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            _telemetryService.TrackApiRequest(
                endpoint, 
                context.User, 
                context.Response.StatusCode, 
                stopwatch.ElapsedMilliseconds);

            // Track specific MCP endpoints with more detail
            if (context.Request.Path.StartsWithSegments("/mcp"))
            {
                TrackMcpEndpoint(context, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Track the error
            var endpoint = $"{context.Request.Method} {context.Request.Path}";
            _telemetryService.TrackApiRequest(
                endpoint, 
                context.User, 
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

    private void TrackMcpEndpoint(HttpContext context, double duration)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        var method = context.Request.Method;

        // Track different MCP endpoint types
        switch (path)
        {
            case "/mcp/tools":
                if (method == "GET")
                {
                    _telemetryService.TrackCustomMetric("MCP.Tools.List", 1, new Dictionary<string, string>
                    {
                        ["UserId"] = GetUserId(context),
                        ["Duration"] = duration.ToString()
                    });
                }
                break;

            case var toolPath when toolPath?.StartsWith("/mcp/tools/") == true:
                if (method == "POST")
                {
                    var toolName = ExtractToolName(path);
                    if (!string.IsNullOrEmpty(toolName))
                    {
                        _telemetryService.TrackCustomMetric("MCP.Tool.Execution", 1, new Dictionary<string, string>
                        {
                            ["ToolName"] = toolName,
                            ["UserId"] = GetUserId(context),
                            ["Duration"] = duration.ToString(),
                            ["StatusCode"] = context.Response.StatusCode.ToString()
                        });
                    }
                }
                break;

            case "/mcp/info":
                _telemetryService.TrackCustomMetric("MCP.Info.Access", 1, new Dictionary<string, string>
                {
                    ["UserId"] = GetUserId(context),
                    ["Duration"] = duration.ToString()
                });
                break;
        }
    }

    private string GetUserId(HttpContext context)
    {
        return context.User?.Identity?.Name 
            ?? context.User?.FindFirst("sub")?.Value 
            ?? context.User?.FindFirst("oid")?.Value 
            ?? "Anonymous";
    }

    private string? ExtractToolName(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 3 ? segments[2] : null;
    }
}