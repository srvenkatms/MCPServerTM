using WeatherAPI.Models;

namespace WeatherAPI.Services;

public interface IConfigurationValidationService
{
    Task<bool> ValidateConfigurationAsync();
    Task<Dictionary<string, object>> GetConfigurationStatusAsync();
}

public class ConfigurationValidationService : IConfigurationValidationService
{
    private readonly McpServerConfig _mcpConfig;
    private readonly AgentFoundryConfig _agentConfig;
    private readonly ILogger<ConfigurationValidationService> _logger;

    public ConfigurationValidationService(
        McpServerConfig mcpConfig,
        AgentFoundryConfig agentConfig,
        ILogger<ConfigurationValidationService> logger)
    {
        _mcpConfig = mcpConfig;
        _agentConfig = agentConfig;
        _logger = logger;
    }

    public async Task<bool> ValidateConfigurationAsync()
    {
        var isValid = true;
        var validationErrors = new List<string>();

        // Validate MCP Server configuration
        if (_mcpConfig.AuthenticationRequired)
        {
            if (string.IsNullOrEmpty(_mcpConfig.ClientId))
            {
                validationErrors.Add("McpServer:ClientId is required when authentication is enabled");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_mcpConfig.ClientSecret))
            {
                validationErrors.Add("McpServer:ClientSecret is required when authentication is enabled");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_mcpConfig.TokenEndpoint))
            {
                validationErrors.Add("McpServer:TokenEndpoint is required when authentication is enabled");
                isValid = false;
            }

            if (string.IsNullOrEmpty(_mcpConfig.Scope))
            {
                validationErrors.Add("McpServer:Scope is required when authentication is enabled");
                isValid = false;
            }

            // Validate token endpoint format
            if (!string.IsNullOrEmpty(_mcpConfig.TokenEndpoint))
            {
                if (_mcpConfig.TokenEndpoint.Contains("{tenant-id}") && string.IsNullOrEmpty(_mcpConfig.TenantId))
                {
                    validationErrors.Add("McpServer:TenantId is required when TokenEndpoint contains {tenant-id} placeholder");
                    isValid = false;
                }

                if (!Uri.TryCreate(_mcpConfig.TokenEndpoint.Replace("{tenant-id}", _mcpConfig.TenantId ?? "test"), UriKind.Absolute, out _))
                {
                    validationErrors.Add("McpServer:TokenEndpoint is not a valid URL");
                    isValid = false;
                }
            }
        }

        if (string.IsNullOrEmpty(_mcpConfig.BaseUrl))
        {
            validationErrors.Add("McpServer:BaseUrl is required");
            isValid = false;
        }
        else if (!Uri.TryCreate(_mcpConfig.BaseUrl, UriKind.Absolute, out _))
        {
            validationErrors.Add("McpServer:BaseUrl is not a valid URL");
            isValid = false;
        }

        // Validate Agent Foundry configuration (less critical for now since it's mock)
        if (string.IsNullOrEmpty(_agentConfig.DefaultAgentName))
        {
            _logger.LogWarning("AgentFoundry:DefaultAgentName is not configured, using default");
        }

        if (validationErrors.Any())
        {
            _logger.LogError("Configuration validation failed: {Errors}", string.Join("; ", validationErrors));
        }
        else
        {
            _logger.LogInformation("Configuration validation passed successfully");
        }

        return isValid;
    }

    public async Task<Dictionary<string, object>> GetConfigurationStatusAsync()
    {
        await Task.Delay(1); // Make async

        var status = new Dictionary<string, object>
        {
            ["Timestamp"] = DateTime.UtcNow,
            ["McpServer"] = new
            {
                BaseUrl = _mcpConfig.BaseUrl,
                AuthenticationRequired = _mcpConfig.AuthenticationRequired,
                ClientId = string.IsNullOrEmpty(_mcpConfig.ClientId) ? "<not configured>" : $"***{_mcpConfig.ClientId[^4..]}",
                ClientSecret = string.IsNullOrEmpty(_mcpConfig.ClientSecret) ? "<not configured>" : "***configured",
                TokenEndpoint = _mcpConfig.TokenEndpoint,
                TenantId = _mcpConfig.TenantId,
                Scope = _mcpConfig.Scope,
                TokenEndpointResolved = !string.IsNullOrEmpty(_mcpConfig.TenantId) && !string.IsNullOrEmpty(_mcpConfig.TokenEndpoint)
                    ? _mcpConfig.TokenEndpoint.Replace("{tenant-id}", _mcpConfig.TenantId)
                    : _mcpConfig.TokenEndpoint
            },
            ["AgentFoundry"] = new
            {
                Endpoint = _agentConfig.Endpoint,
                ApiKey = string.IsNullOrEmpty(_agentConfig.ApiKey) ? "<not configured>" : "***configured",
                DefaultAgentName = _agentConfig.DefaultAgentName,
                ModelDeploymentName = _agentConfig.ModelDeploymentName
            }
        };

        return status;
    }
}