using WeatherAPI.Models;

namespace WeatherAPI.Services;

public interface IAgentFoundryService
{
    Task<string> GetOrCreateAgentAsync(string agentName);
    Task<string> ProcessWeatherRequestAsync(string agentId, string prompt);
}

public class AgentFoundryService : IAgentFoundryService
{
    private readonly AgentFoundryConfig _config;
    private readonly ILogger<AgentFoundryService> _logger;
    private readonly Dictionary<string, string> _agentCache = new();

    public AgentFoundryService(AgentFoundryConfig config, ILogger<AgentFoundryService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> GetOrCreateAgentAsync(string agentName)
    {
        try
        {
            // Check cache first
            if (_agentCache.TryGetValue(agentName, out var cachedAgentId))
            {
                _logger.LogInformation("Using cached agent: {AgentName} -> {AgentId}", agentName, cachedAgentId);
                return cachedAgentId;
            }

            // For now, create a mock agent ID until we can properly integrate with Agent Foundry
            // In a real implementation, this would connect to Azure AI Foundry
            var agentId = $"agent-{agentName}-{Guid.NewGuid().ToString("N")[..8]}";
            _agentCache[agentName] = agentId;
            
            _logger.LogInformation("Created mock agent (Agent Foundry integration pending): {AgentName} -> {AgentId}", agentName, agentId);
            return agentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create agent: {AgentName}", agentName);
            
            // Return a fallback agent ID for development/testing
            var fallbackId = $"fallback-agent-{Guid.NewGuid()}";
            _agentCache[agentName] = fallbackId;
            _logger.LogWarning("Using fallback agent ID: {FallbackId}", fallbackId);
            return fallbackId;
        }
    }

    public async Task<string> ProcessWeatherRequestAsync(string agentId, string prompt)
    {
        try
        {
            // Mock implementation - in a real scenario this would use Agent Foundry SDK
            // to process the prompt through the configured LLM
            await Task.Delay(100); // Simulate processing time
            
            _logger.LogInformation("Processing weather request with agent {AgentId}: {Prompt}", agentId, prompt);
            
            // Return a mock response that acknowledges the request
            return $"Weather request processed by agent {agentId}. The system retrieved weather data as requested.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process weather request with agent: {AgentId}", agentId);
            
            // Return a fallback response for development/testing
            return $"Weather processing temporarily unavailable. Prompt was: {prompt}";
        }
    }
}