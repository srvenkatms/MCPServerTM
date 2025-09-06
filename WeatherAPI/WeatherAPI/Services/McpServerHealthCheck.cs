using Microsoft.Extensions.Diagnostics.HealthChecks;
using WeatherAPI.Models;

namespace WeatherAPI.Services;

public class McpServerHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly McpServerConfig _config;
    private readonly ILogger<McpServerHealthCheck> _logger;

    public McpServerHealthCheck(
        HttpClient httpClient,
        McpServerConfig config,
        ILogger<McpServerHealthCheck> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthUrl = $"{_config.BaseUrl.TrimEnd('/')}/health";
            _logger.LogDebug("Checking MCP server health at {HealthUrl}", healthUrl);

            using var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("MCP server health check successful");
                
                return HealthCheckResult.Healthy(
                    $"MCP server is healthy. Response: {content}");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("MCP server health check failed with status {StatusCode}. Response: {Content}",
                    response.StatusCode, errorContent);
                
                return HealthCheckResult.Unhealthy(
                    $"MCP server returned status {response.StatusCode}. Response: {errorContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during MCP server health check");
            return HealthCheckResult.Unhealthy(
                $"Network error connecting to MCP server: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout during MCP server health check");
            return HealthCheckResult.Unhealthy(
                $"Timeout connecting to MCP server: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during MCP server health check");
            return HealthCheckResult.Unhealthy(
                $"Unexpected error during health check: {ex.Message}");
        }
    }
}