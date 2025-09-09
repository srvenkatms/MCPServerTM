using WeatherAPI.Models;

namespace WeatherAPI.Services;

public interface IAgentFoundryService
{
    Task<string> GetOrCreateAgentAsync(string agentName);
    Task<string> ProcessWeatherRequestAsync(string agentId, string prompt);

    // New methods for grounded weather data via MCP server
    Task<CurrentWeatherInfo?> GetCurrentWeatherAsync(string agentName, string state, string? city = null);
    Task<WeatherForecastInfo[]?> GetWeatherForecastAsync(string agentName, string state, int days = 5);
    Task<WeatherAlertInfo[]?> GetWeatherAlertsAsync(string agentName, string state);
}

public class AgentFoundryService : IAgentFoundryService
{
    private readonly AgentFoundryConfig _config;
    private readonly McpServerConfig _mcpConfig;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentFoundryService> _logger;
    private readonly Dictionary<string, string> _agentCache = new();

    public AgentFoundryService(
        AgentFoundryConfig config,
        McpServerConfig mcpConfig,
        HttpClient httpClient,
        ILogger<AgentFoundryService> logger)
    {
        _config = config;
        _mcpConfig = mcpConfig;
        _httpClient = httpClient;
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

            // Call Azure AI Foundry API to create or get agent
            var endpoint = _config.Endpoint.TrimEnd('/') + "/agents";
            var payload = new { name = agentName, modelDeploymentName = _config.ModelDeploymentName };
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Api-Key", _config.ApiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            string agentId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(agentId))
            {
                throw new Exception($"AgentFoundry response did not contain an agent id. Raw response: {content}");
            }
            _agentCache[agentName] = agentId;
            _logger.LogInformation("Created or retrieved agent from Foundry: {AgentName} -> {AgentId}", agentName, agentId);
            return agentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or create agent from Foundry: {AgentName}", agentName);
            // Return a fallback agent ID for development/testing
            var fallbackId = $"fallback-agent-{Guid.NewGuid()}";
            _agentCache[agentName] = fallbackId;
            _logger.LogWarning("Using fallback agent ID: {FallbackId}", fallbackId);
            await Task.CompletedTask;
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
            await Task.CompletedTask;
            return $"Weather processing temporarily unavailable. Prompt was: {prompt}";
        }
    }

    // New: Get current weather via MCP server, using agent context
    public async Task<CurrentWeatherInfo?> GetCurrentWeatherAsync(string agentName, string state, string? city = null)
    {
        var agentId = await GetOrCreateAgentAsync(agentName);
        var payload = new { agentId, state, city };
        var url = $"{_mcpConfig.BaseUrl}/tools/getcurrentweather";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
        await AddAuthHeaderAsync(request);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("Raw getcurrentweather response: {Content}", content);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            // If this looks like an alerts payload (state + alerts array) bail out early
            if (root.TryGetProperty("alerts", out _) && !root.TryGetProperty("location", out _))
            {
                _logger.LogWarning("getcurrentweather returned alerts schema (keys: {Keys}). Tool mismatch.", string.Join(',', root.EnumerateObject().Select(p => p.Name)));
                return null;
            }

            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var cw = System.Text.Json.JsonSerializer.Deserialize<CurrentWeatherInfo>(content, options);
            return cw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize getcurrentweather response into CurrentWeatherInfo. Returning null.");
            return null;
        }
    }

    // New: Get weather forecast via MCP server, using agent context
    public async Task<WeatherForecastInfo[]?> GetWeatherForecastAsync(string agentName, string state, int days = 5)
    {
        var agentId = await GetOrCreateAgentAsync(agentName);
        var payload = new { agentId, state, days };
        var url = $"{_mcpConfig.BaseUrl}/tools/getweatherforecast";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
        await AddAuthHeaderAsync(request);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (root.TryGetProperty("forecast", out var forecastArray) && forecastArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return System.Text.Json.JsonSerializer.Deserialize<WeatherForecastInfo[]>(forecastArray.GetRawText());
        }
        _logger.LogWarning("Weather forecast response did not contain a 'forecast' array. Raw response: {Content}", content);
        return null;
    }

    // New: Get weather alerts via MCP server, using agent context
    public async Task<WeatherAlertInfo[]?> GetWeatherAlertsAsync(string agentName, string state)
    {
        var agentId = await GetOrCreateAgentAsync(agentName);
        var payload = new { agentId, state };
        var url = $"{_mcpConfig.BaseUrl}/tools/getweatheralerts";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
        await AddAuthHeaderAsync(request);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(content);
        var root = doc.RootElement;
        if (root.TryGetProperty("alerts", out var alertsArray) && alertsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            return System.Text.Json.JsonSerializer.Deserialize<WeatherAlertInfo[]>(alertsArray.GetRawText());
        }
        _logger.LogWarning("Weather alerts response did not contain an 'alerts' array. Raw response: {Content}", content);
        return null;
    }

    // Helper: Add authentication header if required
    private async Task AddAuthHeaderAsync(HttpRequestMessage request)
    {
        if (_mcpConfig.AuthenticationRequired)
        {
            // TODO: Implement OAuth2 token retrieval as in McpClientService
            // For now, add a placeholder header
            request.Headers.Add("Authorization", $"Bearer {await GetAccessTokenAsync()}");
        }
    }

    // Full token retrieval logic (copied and adapted from McpClientService)
    private string? _accessToken;
    private DateTime _tokenExpiry;

    private async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        if (!_mcpConfig.AuthenticationRequired)
        {
            return string.Empty;
        }

        // Validate required configuration before making request
        if (string.IsNullOrEmpty(_mcpConfig.ClientId))
        {
            throw new InvalidOperationException("ClientId is required for authentication but not configured");
        }
        if (string.IsNullOrEmpty(_mcpConfig.ClientSecret))
        {
            throw new InvalidOperationException("ClientSecret is required for authentication but not configured");
        }
        if (string.IsNullOrEmpty(_mcpConfig.TokenEndpoint))
        {
            throw new InvalidOperationException("TokenEndpoint is required for authentication but not configured");
        }
        if (string.IsNullOrEmpty(_mcpConfig.Scope))
        {
            throw new InvalidOperationException("Scope is required for authentication but not configured");
        }

        try
        {
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _mcpConfig.ClientId),
                new KeyValuePair<string, string>("client_secret", _mcpConfig.ClientSecret),
                new KeyValuePair<string, string>("scope", _mcpConfig.Scope)
            });

            // Replace {tenant-id} placeholder in TokenEndpoint if TenantId is provided
            var tokenEndpoint = _mcpConfig.TokenEndpoint;
            if (!string.IsNullOrEmpty(_mcpConfig.TenantId) && tokenEndpoint.Contains("{tenant-id}"))
            {
                tokenEndpoint = tokenEndpoint.Replace("{tenant-id}", _mcpConfig.TenantId);
            }

            _logger.LogDebug("Requesting OAuth token from endpoint: {TokenEndpoint} with ClientId: {ClientId} and Scope: {Scope}", 
                tokenEndpoint, _mcpConfig.ClientId, _mcpConfig.Scope);

            var response = await _httpClient.PostAsync(tokenEndpoint, tokenRequest);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            _accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // Subtract 60s for safety
            return _accessToken ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve OAuth token");
            throw;
        }
    }
}
