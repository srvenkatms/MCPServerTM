using WeatherAPI.Models;
using System.Diagnostics;

namespace WeatherAPI.Services;

public interface IWeatherService
{
    Task<WeatherResponse> GetWeatherAsync(WeatherRequest request);
}

public class WeatherService : IWeatherService
{
    private readonly IMcpClientService _mcpClient;
    private readonly IAgentFoundryService _agentFoundry;
    private readonly WeatherPromptsConfig _prompts;
    private readonly AgentFoundryConfig _agentConfig;
    private readonly ILogger<WeatherService> _logger;
    private readonly WeatherTelemetryService _telemetryService;

    public WeatherService(
        IMcpClientService mcpClient,
        IAgentFoundryService agentFoundry,
        WeatherPromptsConfig prompts,
        AgentFoundryConfig agentConfig,
        ILogger<WeatherService> logger,
        WeatherTelemetryService telemetryService)
    {
        _mcpClient = mcpClient;
        _agentFoundry = agentFoundry;
        _prompts = prompts;
        _agentConfig = agentConfig;
        _logger = logger;
        _telemetryService = telemetryService;
    }

    public async Task<WeatherResponse> GetWeatherAsync(WeatherRequest request)
    {
        var overallStopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing weather request for city: {City}", request.City);

            // Get or create agent with dependency tracking
            var agentName = _agentConfig.DefaultAgentName;
            var agentStopwatch = Stopwatch.StartNew();
            var agentId = await _agentFoundry.GetOrCreateAgentAsync(agentName);
            agentStopwatch.Stop();
            
            _telemetryService.TrackDependency("AgentFoundry", "GetOrCreateAgent", true, agentStopwatch.ElapsedMilliseconds, "200");

            // Determine state from city if not provided
            var state = request.State ?? await DetermineStateFromCityAsync(request.City);

            // Create tasks to fetch all weather data in parallel via AgentFoundryService
            var currentWeatherStopwatch = Stopwatch.StartNew();
            var currentWeatherTask = _agentFoundry.GetCurrentWeatherAsync(agentName, state, request.City);
            
            var forecastStopwatch = Stopwatch.StartNew();
            var forecastTask = _agentFoundry.GetWeatherForecastAsync(agentName, state, request.Days);
            
            var alertsStopwatch = Stopwatch.StartNew();
            var alertsTask = _agentFoundry.GetWeatherAlertsAsync(agentName, state);

            // Wait for all tasks to complete
            await Task.WhenAll(currentWeatherTask, forecastTask, alertsTask);
            
            // Track dependency calls
            currentWeatherStopwatch.Stop();
            forecastStopwatch.Stop();
            alertsStopwatch.Stop();
            
            _telemetryService.TrackDependency("AgentFoundry", "GetCurrentWeather", currentWeatherTask.IsCompletedSuccessfully, currentWeatherStopwatch.ElapsedMilliseconds);
            _telemetryService.TrackDependency("AgentFoundry", "GetWeatherForecast", forecastTask.IsCompletedSuccessfully, forecastStopwatch.ElapsedMilliseconds);
            _telemetryService.TrackDependency("AgentFoundry", "GetWeatherAlerts", alertsTask.IsCompletedSuccessfully, alertsStopwatch.ElapsedMilliseconds);

            var response = new WeatherResponse
            {
                City = request.City,
                State = state,
                CurrentWeather = await currentWeatherTask,
                Forecast = await forecastTask,
                Alerts = await alertsTask,
                AgentId = agentId,
                RetrievedAt = DateTime.UtcNow
            };

            // Process with agent foundry for enhanced responses (optional)
            if (!string.IsNullOrEmpty(_prompts.CurrentWeatherTemplate) && response.CurrentWeather != null)
            {
                var promptStopwatch = Stopwatch.StartNew();
                var prompt = _prompts.CurrentWeatherTemplate
                    .Replace("{city}", request.City)
                    .Replace("{state}", state);
                var agentResponse = await _agentFoundry.ProcessWeatherRequestAsync(agentId, prompt);
                promptStopwatch.Stop();
                
                _telemetryService.TrackDependency("AgentFoundry", "ProcessWeatherRequest", true, promptStopwatch.ElapsedMilliseconds);
                _logger.LogDebug("Agent response for current weather: {Response}", agentResponse);
            }

            overallStopwatch.Stop();
            _logger.LogInformation("Successfully processed weather request for {City}, {State} in {Duration}ms", 
                request.City, state, overallStopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();
            _logger.LogError(ex, "Failed to process weather request for city: {City} after {Duration}ms", 
                request.City, overallStopwatch.ElapsedMilliseconds);
                
            // Track failed dependency
            _telemetryService.TrackDependency("WeatherService", "GetWeatherAsync", false, overallStopwatch.ElapsedMilliseconds, ex.GetType().Name);
            
            return new WeatherResponse
            {
                City = request.City,
                State = request.State ?? "Unknown",
                AgentId = "error",
                RetrievedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<string> DetermineStateFromCityAsync(string city)
    {
        // Simple mapping for major cities - in a real implementation, 
        // this could use a geocoding service or database lookup
        var cityToStateMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["New York"] = "NY", ["Los Angeles"] = "CA", ["Chicago"] = "IL", 
            ["Houston"] = "TX", ["Phoenix"] = "AZ", ["Philadelphia"] = "PA",
            ["San Antonio"] = "TX", ["San Diego"] = "CA", ["Dallas"] = "TX",
            ["San Jose"] = "CA", ["Austin"] = "TX", ["Jacksonville"] = "FL",
            ["Fort Worth"] = "TX", ["Columbus"] = "OH", ["Charlotte"] = "NC",
            ["San Francisco"] = "CA", ["Indianapolis"] = "IN", ["Seattle"] = "WA",
            ["Denver"] = "CO", ["Washington"] = "DC", ["Boston"] = "MA",
            ["El Paso"] = "TX", ["Detroit"] = "MI", ["Nashville"] = "TN",
            ["Portland"] = "OR", ["Memphis"] = "TN", ["Oklahoma City"] = "OK",
            ["Las Vegas"] = "NV", ["Louisville"] = "KY", ["Baltimore"] = "MD",
            ["Milwaukee"] = "WI", ["Albuquerque"] = "NM", ["Tucson"] = "AZ",
            ["Fresno"] = "CA", ["Sacramento"] = "CA", ["Mesa"] = "AZ",
            ["Kansas City"] = "MO", ["Atlanta"] = "GA", ["Long Beach"] = "CA",
            ["Colorado Springs"] = "CO", ["Raleigh"] = "NC", ["Miami"] = "FL",
            ["Virginia Beach"] = "VA", ["Omaha"] = "NE", ["Oakland"] = "CA",
            ["Minneapolis"] = "MN", ["Tulsa"] = "OK", ["Arlington"] = "TX",
            ["Tampa"] = "FL", ["New Orleans"] = "LA", ["Wichita"] = "KS",
            ["Cleveland"] = "OH", ["Bakersfield"] = "CA", ["Aurora"] = "CO",
            ["Anaheim"] = "CA", ["Honolulu"] = "HI", ["Santa Ana"] = "CA",
            ["Riverside"] = "CA", ["Corpus Christi"] = "TX", ["Lexington"] = "KY",
            ["Stockton"] = "CA", ["Henderson"] = "NV", ["Saint Paul"] = "MN",
            ["St. Paul"] = "MN", ["Cincinnati"] = "OH", ["Pittsburgh"] = "PA"
        };

        await Task.Delay(1); // Make this async for consistency
        
        return cityToStateMap.TryGetValue(city, out var state) ? state : "CA"; // Default to CA if not found
    }
}
