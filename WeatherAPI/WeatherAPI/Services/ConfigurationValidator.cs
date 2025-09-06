using Microsoft.Extensions.Diagnostics.HealthChecks;
using WeatherAPI.Models;

namespace WeatherAPI.Services;

public class ConfigurationValidator : IHealthCheck
{
    private readonly McpServerConfig _mcpConfig;
    private readonly AgentFoundryConfig _agentConfig;
    private readonly ILogger<ConfigurationValidator> _logger;

    public ConfigurationValidator(
        McpServerConfig mcpConfig,
        AgentFoundryConfig agentConfig,
        ILogger<ConfigurationValidator> logger)
    {
        _mcpConfig = mcpConfig;
        _agentConfig = agentConfig;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Validate MCP Server Configuration
        if (string.IsNullOrWhiteSpace(_mcpConfig.BaseUrl))
            errors.Add("McpServer:BaseUrl is required");

        if (_mcpConfig.AuthenticationRequired)
        {
            if (string.IsNullOrWhiteSpace(_mcpConfig.ClientId))
                errors.Add("McpServer:ClientId is required when authentication is enabled");
            
            if (string.IsNullOrWhiteSpace(_mcpConfig.ClientSecret))
                errors.Add("McpServer:ClientSecret is required when authentication is enabled");
            
            if (string.IsNullOrWhiteSpace(_mcpConfig.TokenEndpoint))
                errors.Add("McpServer:TokenEndpoint is required when authentication is enabled");
            
            if (string.IsNullOrWhiteSpace(_mcpConfig.Scope))
                errors.Add("McpServer:Scope is required when authentication is enabled");
        }

        // Validate Agent Foundry Configuration
        if (string.IsNullOrWhiteSpace(_agentConfig.Endpoint))
            errors.Add("AgentFoundry:Endpoint is required");
        
        if (string.IsNullOrWhiteSpace(_agentConfig.ApiKey))
            errors.Add("AgentFoundry:ApiKey is required");
        
        if (string.IsNullOrWhiteSpace(_agentConfig.DefaultAgentName))
            errors.Add("AgentFoundry:DefaultAgentName is required");
        
        if (string.IsNullOrWhiteSpace(_agentConfig.ModelDeploymentName))
            errors.Add("AgentFoundry:ModelDeploymentName is required");

        if (errors.Any())
        {
            var errorMessage = string.Join("; ", errors);
            _logger.LogError("Configuration validation failed: {Errors}", errorMessage);
            return Task.FromResult(HealthCheckResult.Unhealthy($"Configuration errors: {errorMessage}"));
        }

        _logger.LogDebug("Configuration validation passed");
        return Task.FromResult(HealthCheckResult.Healthy("All required configuration settings are present"));
    }
}