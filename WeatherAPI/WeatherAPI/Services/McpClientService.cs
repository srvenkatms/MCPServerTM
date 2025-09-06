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
    private string? _accessToken;
    private DateTime _tokenExpiry;

    public McpClientService(
        HttpClient httpClient, 
        McpServerConfig config, 
        ILogger<McpClientService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
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

            _logger.LogInformation("Requesting access token from {TokenEndpoint}", tokenEndpoint);
            
            var response = await _httpClient.PostAsync(tokenEndpoint, tokenRequest);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Token request failed with status {StatusCode}. Response: {Content}", 
                    response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode(); // This will throw with the details above
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
            _logger.LogError(ex, "Failed to obtain access token from {TokenEndpoint}", tokenEndpoint);
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

        if (data != null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(data), 
                System.Text.Encoding.UTF8, 
                "application/json");
        }

        return request;
    }

    public async Task<CurrentWeatherInfo?> GetCurrentWeatherAsync(string state, string? city = null)
    {
        try
        {
            var payload = new { state = state, city = city };
            var request = await CreateAuthenticatedRequestAsync("mcp/tools/getcurrentweather", HttpMethod.Post, payload);
            
            _logger.LogInformation("Sending weather request to MCP server: {Url}", request.RequestUri);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("MCP server returned {StatusCode} for current weather request. Response: {Content}", 
                    response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received weather response from MCP server: {Content}", content);
            
            var weatherData = JsonSerializer.Deserialize<JsonElement>(content);
            return MapToCurrentWeatherInfo(weatherData);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error connecting to MCP server for current weather. State: {State}, City: {City}, BaseUrl: {BaseUrl}", 
                state, city, _config.BaseUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout connecting to MCP server for current weather. State: {State}, City: {City}", state, city);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse weather response from MCP server. State: {State}, City: {City}", state, city);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting current weather for {State}, {City}", state, city);
            return null;
        }
    }

    public async Task<WeatherForecastInfo[]?> GetWeatherForecastAsync(string state, int days = 5)
    {
        try
        {
            var payload = new { state = state, days = days };
            var request = await CreateAuthenticatedRequestAsync("mcp/tools/getweatherforecast", HttpMethod.Post, payload);
            
            _logger.LogInformation("Sending forecast request to MCP server: {Url}", request.RequestUri);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("MCP server returned {StatusCode} for forecast request. Response: {Content}", 
                    response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received forecast response from MCP server: {Content}", content);
            
            var forecastData = JsonSerializer.Deserialize<JsonElement>(content);
            return MapToWeatherForecastInfo(forecastData);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error connecting to MCP server for forecast. State: {State}, BaseUrl: {BaseUrl}", 
                state, _config.BaseUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout connecting to MCP server for forecast. State: {State}", state);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse forecast response from MCP server. State: {State}", state);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting weather forecast for {State}", state);
            return null;
        }
    }

    public async Task<WeatherAlertInfo[]?> GetWeatherAlertsAsync(string state)
    {
        try
        {
            var payload = new { state = state };
            var request = await CreateAuthenticatedRequestAsync("mcp/tools/getweatheralerts", HttpMethod.Post, payload);
            
            _logger.LogInformation("Sending alerts request to MCP server: {Url}", request.RequestUri);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("MCP server returned {StatusCode} for alerts request. Response: {Content}", 
                    response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received alerts response from MCP server: {Content}", content);
            
            var alertsData = JsonSerializer.Deserialize<JsonElement>(content);
            return MapToWeatherAlertInfo(alertsData);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error connecting to MCP server for alerts. State: {State}, BaseUrl: {BaseUrl}", 
                state, _config.BaseUrl);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout connecting to MCP server for alerts. State: {State}", state);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse alerts response from MCP server. State: {State}", state);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting weather alerts for {State}", state);
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