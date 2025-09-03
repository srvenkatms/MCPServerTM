using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace MCPServer.Tools;

[McpServerToolType]
public sealed class WeatherTools
{
    [McpServerTool, Description("Get weather alerts for a US state.")]
    public static async Task<object> GetWeatherAlerts(
        [Description("The US state code (e.g., 'CA', 'TX', 'NY')")]
        string state)
    {
        // Simulate async weather API call
        await Task.Delay(100);
        
        // Mock weather alerts data
        var alerts = new[]
        {
            new
            {
                id = Guid.NewGuid().ToString(),
                state = state.ToUpperInvariant(),
                alertType = "Heat Advisory",
                severity = "Moderate",
                description = $"Excessive heat warning for {GetStateName(state)}. Temperatures may reach above 95°F.",
                startTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endTime = DateTime.UtcNow.AddHours(24).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                areas = new[] { "Metro Area", "Central Region" }
            },
            new
            {
                id = Guid.NewGuid().ToString(),
                state = state.ToUpperInvariant(),
                alertType = "Thunderstorm Watch",
                severity = "Minor",
                description = $"Possible thunderstorms in {GetStateName(state)} this evening.",
                startTime = DateTime.UtcNow.AddHours(6).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                endTime = DateTime.UtcNow.AddHours(12).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                areas = new[] { "Northern Region" }
            }
        };

        return new
        {
            state = state.ToUpperInvariant(),
            stateName = GetStateName(state),
            alertCount = alerts.Length,
            alerts = alerts,
            retrievedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    [McpServerTool, Description("Get current weather conditions for a US state.")]
    public static async Task<object> GetCurrentWeather(
        [Description("The US state code (e.g., 'CA', 'TX', 'NY')")]
        string state,
        [Description("The city name (optional)")]
        string? city = null)
    {
        // Simulate async weather API call
        await Task.Delay(150);

        var location = string.IsNullOrEmpty(city) ? GetStateName(state) : $"{city}, {GetStateName(state)}";
        var random = new Random();

        return new
        {
            location = location,
            state = state.ToUpperInvariant(),
            temperature = new
            {
                current = random.Next(60, 95),
                unit = "°F",
                feelsLike = random.Next(65, 100)
            },
            conditions = new
            {
                description = GetRandomCondition(),
                humidity = random.Next(30, 80),
                windSpeed = random.Next(5, 25),
                windDirection = GetRandomDirection(),
                visibility = random.Next(5, 15)
            },
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    [McpServerTool, Description("Get weather forecast for a US state.")]
    public static async Task<object> GetWeatherForecast(
        [Description("The US state code (e.g., 'CA', 'TX', 'NY')")]
        string state,
        [Description("Number of days to forecast (1-7)")]
        int days = 5)
    {
        // Simulate async weather API call
        await Task.Delay(200);

        if (days < 1 || days > 7)
        {
            throw new ArgumentException("Days must be between 1 and 7");
        }

        var random = new Random();
        var forecast = Enumerable.Range(1, days).Select(day => new
        {
            date = DateTime.UtcNow.AddDays(day).ToString("yyyy-MM-dd"),
            dayOfWeek = DateTime.UtcNow.AddDays(day).DayOfWeek.ToString(),
            temperature = new
            {
                high = random.Next(75, 95),
                low = random.Next(55, 75),
                unit = "°F"
            },
            conditions = GetRandomCondition(),
            precipitationChance = random.Next(0, 100),
            windSpeed = random.Next(5, 20)
        }).ToArray();

        return new
        {
            state = state.ToUpperInvariant(),
            stateName = GetStateName(state),
            forecastDays = days,
            forecast = forecast,
            retrievedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };
    }

    private static string GetStateName(string stateCode)
    {
        var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AL"] = "Alabama", ["AK"] = "Alaska", ["AZ"] = "Arizona", ["AR"] = "Arkansas",
            ["CA"] = "California", ["CO"] = "Colorado", ["CT"] = "Connecticut", ["DE"] = "Delaware",
            ["FL"] = "Florida", ["GA"] = "Georgia", ["HI"] = "Hawaii", ["ID"] = "Idaho",
            ["IL"] = "Illinois", ["IN"] = "Indiana", ["IA"] = "Iowa", ["KS"] = "Kansas",
            ["KY"] = "Kentucky", ["LA"] = "Louisiana", ["ME"] = "Maine", ["MD"] = "Maryland",
            ["MA"] = "Massachusetts", ["MI"] = "Michigan", ["MN"] = "Minnesota", ["MS"] = "Mississippi",
            ["MO"] = "Missouri", ["MT"] = "Montana", ["NE"] = "Nebraska", ["NV"] = "Nevada",
            ["NH"] = "New Hampshire", ["NJ"] = "New Jersey", ["NM"] = "New Mexico", ["NY"] = "New York",
            ["NC"] = "North Carolina", ["ND"] = "North Dakota", ["OH"] = "Ohio", ["OK"] = "Oklahoma",
            ["OR"] = "Oregon", ["PA"] = "Pennsylvania", ["RI"] = "Rhode Island", ["SC"] = "South Carolina",
            ["SD"] = "South Dakota", ["TN"] = "Tennessee", ["TX"] = "Texas", ["UT"] = "Utah",
            ["VT"] = "Vermont", ["VA"] = "Virginia", ["WA"] = "Washington", ["WV"] = "West Virginia",
            ["WI"] = "Wisconsin", ["WY"] = "Wyoming"
        };

        return states.TryGetValue(stateCode, out var name) ? name : stateCode.ToUpperInvariant();
    }

    private static string GetRandomCondition()
    {
        var conditions = new[] { "Sunny", "Partly Cloudy", "Cloudy", "Light Rain", "Heavy Rain", "Thunderstorms", "Clear", "Overcast" };
        return conditions[new Random().Next(conditions.Length)];
    }

    private static string GetRandomDirection()
    {
        var directions = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return directions[new Random().Next(directions.Length)];
    }
}