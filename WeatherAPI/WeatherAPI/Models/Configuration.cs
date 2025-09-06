using System.ComponentModel.DataAnnotations;

namespace WeatherAPI.Models;

public class AgentFoundryConfig
{
    [Required]
    public string Endpoint { get; set; } = string.Empty;
    
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    [Required]
    public string DefaultAgentName { get; set; } = string.Empty;
    
    [Required]
    public string ModelDeploymentName { get; set; } = string.Empty;
}

public class McpServerConfig
{
    [Required]
    public string BaseUrl { get; set; } = string.Empty;
    
    public bool AuthenticationRequired { get; set; } = true;
    
    public string ClientId { get; set; } = string.Empty;
    
    public string ClientSecret { get; set; } = string.Empty;
    
    public string TokenEndpoint { get; set; } = string.Empty;
    
    public string TenantId { get; set; } = string.Empty;
    
    public string Scope { get; set; } = string.Empty;
}

public class WeatherPromptsConfig
{
    public string CurrentWeatherTemplate { get; set; } = string.Empty;
    
    public string ForecastTemplate { get; set; } = string.Empty;
    
    public string AlertsTemplate { get; set; } = string.Empty;
}

public class RetryConfig
{
    public int MaxRetries { get; set; } = 3;
    
    public int DelayMilliseconds { get; set; } = 1000;
    
    public bool UseExponentialBackoff { get; set; } = true;
}