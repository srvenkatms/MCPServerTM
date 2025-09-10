using System.Text.Json;
using WeatherAPI.Models;

namespace WeatherAPI.Services;

public interface IMcpClientService
{
    Task<CurrentWeatherInfo?> GetCurrentWeatherAsync(string state, string? city = null);
    Task<WeatherForecastInfo[]?> GetWeatherForecastAsync(string state, int days = 5);
    Task<WeatherAlertInfo[]?> GetWeatherAlertsAsync(string state);
}

public class McpClientService : IMcpClientService
{
    private readonly HttpClient _httpClient;
    private readonly McpServerConfig _config;
    private readonly ILogger<McpClientService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _accessToken;
    private DateTime _tokenExpiry;

    public McpClientService(
        HttpClient httpClient, 
        McpServerConfig config, 
        ILogger<McpClientService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        if (!_config.AuthenticationRequired)
        {
            return string.Empty;
        }

        // Validate required configuration before making request
        if (string.IsNullOrEmpty(_config.ClientId))
        {
            throw new InvalidOperationException("ClientId is required for authentication but not configured");
        }
        if (string.IsNullOrEmpty(_config.ClientSecret))
        {
            throw new InvalidOperationException("ClientSecret is required for authentication but not configured");
        }
        if (string.IsNullOrEmpty(_config.TokenEndpoint))
        {
            throw new InvalidOperationException("TokenEndpoint is required for authentication but not configured");
        }
        if (string.IsNullOrEmpty(_config.Scope))
        {
            throw new InvalidOperationException("Scope is required for authentication but not configured");
        }

        try
        {
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _config.ClientId),
                new KeyValuePair<string, string>("client_secret", _config.ClientSecret),
                new KeyValuePair<string, string>("scope", _config.Scope)
            });

            // Replace {tenant-id} placeholder in TokenEndpoint if TenantId is provided
            var tokenEndpoint = _config.TokenEndpoint;
            if (!string.IsNullOrEmpty(_config.TenantId) && tokenEndpoint.Contains("{tenant-id}"))
            {
                tokenEndpoint = tokenEndpoint.Replace("{tenant-id}", _config.TenantId);
            }

            _logger.LogDebug("Requesting OAuth token from endpoint: {TokenEndpoint} with ClientId: {ClientId} and Scope: {Scope}", 
                tokenEndpoint, _config.ClientId, _config.Scope);

            var response = await _httpClient.PostAsync(tokenEndpoint, tokenRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OAuth token request failed with status {StatusCode} ({ReasonPhrase}). Response: {ErrorContent}", 
                    response.StatusCode, response.ReasonPhrase, errorContent);
                
                // Try to parse error details if it's JSON
                try
                {
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent);
                    if (errorJson.TryGetProperty("error", out var errorCode) && errorJson.TryGetProperty("error_description", out var errorDesc))
                    {
                        throw new HttpRequestException($"OAuth error: {errorCode.GetString()} - {errorDesc.GetString()}. Status: {response.StatusCode}");
                    }
                }
                catch (JsonException)
                {
                    // If not JSON, just throw with the raw content
                }
                
                throw new HttpRequestException($"Failed to obtain OAuth token. Status: {response.StatusCode}, Response: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<JsonElement>(content);

            _accessToken = tokenResponse.GetProperty("access_token").GetString()!;
            var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // Refresh 1 minute early

            _logger.LogInformation("Successfully obtained access token, expires in {ExpiresIn} seconds", expiresIn);
            return _accessToken;
        }
        catch (Exception ex)
        {
            // Use the resolved token endpoint in the error message
            var tokenEndpoint = _config.TokenEndpoint;
            if (!string.IsNullOrEmpty(_config.TenantId) && tokenEndpoint.Contains("{tenant-id}"))
            {
                tokenEndpoint = tokenEndpoint.Replace("{tenant-id}", _config.TenantId);
            }
            
            _logger.LogError(ex, "Failed to obtain access token from {TokenEndpoint}. Config - ClientId: {ClientId}, Scope: {Scope}, TenantId: {TenantId}", 
                tokenEndpoint, _config.ClientId, _config.Scope, _config.TenantId ?? "null");
            throw;
        }
    }

    private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(string endpoint, HttpMethod method, object? data = null)
    {
        var request = new HttpRequestMessage(method, $"{_config.BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}");

        if (_config.AuthenticationRequired)
        {
            var token = await GetAccessTokenAsync();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        // Propagate correlation ID from current request context
        var correlationId = GetCorrelationIdFromContext();
        if (!string.IsNullOrEmpty(correlationId))
        {
            request.Headers.Add("x-correlation-id", correlationId);
            _logger.LogDebug("Propagating correlation ID to MCP request: {CorrelationId}", correlationId);
        }

        if (data != null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(data), 
                System.Text.Encoding.UTF8, 
                "application/json");
        }

        return request;
    }

    private string? GetCorrelationIdFromContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        // Try to get correlation ID from context items first (set by middleware)
        var correlationId = httpContext.Items["CorrelationId"]?.ToString();
        if (!string.IsNullOrEmpty(correlationId))
            return correlationId;

        // Fallback to checking headers
        correlationId = httpContext.Request.Headers["x-correlation-id"].FirstOrDefault()
            ?? httpContext.Request.Headers["Request-ID"].FirstOrDefault()
            ?? httpContext.Request.Headers["x-request-id"].FirstOrDefault()
            ?? httpContext.Request.Headers["x-ms-request-id"].FirstOrDefault();

        return correlationId;
    }

    public async Task<CurrentWeatherInfo?> GetCurrentWeatherAsync(string state, string? city = null)
    {
        try
        {
            var payload = new { state = state, city = city };
            var request = await CreateAuthenticatedRequestAsync("tools/getcurrentweather", HttpMethod.Post, payload);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<JsonElement>(content);

            return MapToCurrentWeatherInfo(weatherData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current weather for {State}, {City}", state, city);
            return null;
        }
    }

    public async Task<WeatherForecastInfo[]?> GetWeatherForecastAsync(string state, int days = 5)
    {
        try
        {
            var payload = new { state = state, days = days };
            var request = await CreateAuthenticatedRequestAsync("tools/getweatherforecast", HttpMethod.Post, payload);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var forecastData = JsonSerializer.Deserialize<JsonElement>(content);

            return MapToWeatherForecastInfo(forecastData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get weather forecast for {State}", state);
            return null;
        }
    }

    public async Task<WeatherAlertInfo[]?> GetWeatherAlertsAsync(string state)
    {
        try
        {
            var payload = new { state = state };
            var request = await CreateAuthenticatedRequestAsync("tools/getweatheralerts", HttpMethod.Post, payload);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var alertsData = JsonSerializer.Deserialize<JsonElement>(content);

            return MapToWeatherAlertInfo(alertsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get weather alerts for {State}", state);
            return null;
        }
    }

    private CurrentWeatherInfo MapToCurrentWeatherInfo(JsonElement data)
    {
        var temperature = data.GetProperty("temperature");
        var conditions = data.GetProperty("conditions");
        
        return new CurrentWeatherInfo
        {
            Location = data.TryGetProperty("location", out var loc) ? loc.GetString() ?? "" : "",
            Temperature = new TemperatureInfo
            {
                Current = temperature.TryGetProperty("current", out var curr) ? curr.GetInt32() : 0,
                Unit = temperature.TryGetProperty("unit", out var unit) ? unit.GetString() ?? "°F" : "°F",
                FeelsLike = temperature.TryGetProperty("feelsLike", out var feels) ? feels.GetInt32() : 0
            },
            Conditions = new WeatherConditionsInfo
            {
                Description = conditions.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                Humidity = conditions.TryGetProperty("humidity", out var humid) ? humid.GetInt32() : 0,
                WindSpeed = conditions.TryGetProperty("windSpeed", out var wind) ? wind.GetInt32() : 0,
                WindDirection = conditions.TryGetProperty("windDirection", out var dir) ? dir.GetString() ?? "" : "",
                Visibility = conditions.TryGetProperty("visibility", out var vis) ? vis.GetInt32() : 0
            },
            Timestamp = data.TryGetProperty("timestamp", out var ts) && DateTime.TryParse(ts.GetString(), out var timestamp) 
                ? timestamp : DateTime.UtcNow
        };
    }

    private WeatherForecastInfo[] MapToWeatherForecastInfo(JsonElement data)
    {
        if (!data.TryGetProperty("forecast", out var forecastArray) || forecastArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WeatherForecastInfo>();
        }

        return forecastArray.EnumerateArray()
            .Select(item => new WeatherForecastInfo
            {
                Date = item.TryGetProperty("date", out var date) ? date.GetString() ?? "" : "",
                DayOfWeek = item.TryGetProperty("dayOfWeek", out var dow) ? dow.GetString() ?? "" : "",
                Temperature = item.TryGetProperty("temperature", out var temp) ? new TemperatureInfo
                {
                    High = temp.TryGetProperty("high", out var high) ? high.GetInt32() : null,
                    Low = temp.TryGetProperty("low", out var low) ? low.GetInt32() : null,
                    Unit = temp.TryGetProperty("unit", out var unit) ? unit.GetString() ?? "°F" : "°F"
                } : new TemperatureInfo(),
                Conditions = item.TryGetProperty("conditions", out var cond) ? cond.GetString() ?? "" : "",
                PrecipitationChance = item.TryGetProperty("precipitationChance", out var precip) ? precip.GetInt32() : 0,
                WindSpeed = item.TryGetProperty("windSpeed", out var wind) ? wind.GetInt32() : 0
            })
            .ToArray();
    }

    private WeatherAlertInfo[] MapToWeatherAlertInfo(JsonElement data)
    {
        if (!data.TryGetProperty("alerts", out var alertsArray) || alertsArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WeatherAlertInfo>();
        }

        return alertsArray.EnumerateArray()
            .Select(item => new WeatherAlertInfo
            {
                Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                AlertType = item.TryGetProperty("alertType", out var type) ? type.GetString() ?? "" : "",
                Severity = item.TryGetProperty("severity", out var sev) ? sev.GetString() ?? "" : "",
                Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                StartTime = item.TryGetProperty("startTime", out var start) && DateTime.TryParse(start.GetString(), out var startTime) 
                    ? startTime : DateTime.MinValue,
                EndTime = item.TryGetProperty("endTime", out var end) && DateTime.TryParse(end.GetString(), out var endTime) 
                    ? endTime : DateTime.MinValue,
                Areas = item.TryGetProperty("areas", out var areas) && areas.ValueKind == JsonValueKind.Array
                    ? areas.EnumerateArray().Select(a => a.GetString() ?? "").ToArray()
                    : Array.Empty<string>()
            })
            .ToArray();
    }
}
