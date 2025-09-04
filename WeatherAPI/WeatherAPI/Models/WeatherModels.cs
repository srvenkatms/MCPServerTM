using System.ComponentModel.DataAnnotations;

namespace WeatherAPI.Models;

public class WeatherRequest
{
    [Required]
    public string City { get; set; } = string.Empty;
    
    public string? State { get; set; }
    
    public int Days { get; set; } = 5;
}

public class WeatherResponse
{
    public string City { get; set; } = string.Empty;
    
    public string State { get; set; } = string.Empty;
    
    public CurrentWeatherInfo? CurrentWeather { get; set; }
    
    public WeatherForecastInfo[]? Forecast { get; set; }
    
    public WeatherAlertInfo[]? Alerts { get; set; }
    
    public string AgentId { get; set; } = string.Empty;
    
    public DateTime RetrievedAt { get; set; }
}

public class CurrentWeatherInfo
{
    public string Location { get; set; } = string.Empty;
    
    public TemperatureInfo Temperature { get; set; } = new();
    
    public WeatherConditionsInfo Conditions { get; set; } = new();
    
    public DateTime Timestamp { get; set; }
}

public class TemperatureInfo
{
    public int Current { get; set; }
    
    public string Unit { get; set; } = "°F";
    
    public int FeelsLike { get; set; }
    
    public int? High { get; set; }
    
    public int? Low { get; set; }
}

public class WeatherConditionsInfo
{
    public string Description { get; set; } = string.Empty;
    
    public int Humidity { get; set; }
    
    public int WindSpeed { get; set; }
    
    public string WindDirection { get; set; } = string.Empty;
    
    public int Visibility { get; set; }
    
    public int PrecipitationChance { get; set; }
}

public class WeatherForecastInfo
{
    public string Date { get; set; } = string.Empty;
    
    public string DayOfWeek { get; set; } = string.Empty;
    
    public TemperatureInfo Temperature { get; set; } = new();
    
    public string Conditions { get; set; } = string.Empty;
    
    public int PrecipitationChance { get; set; }
    
    public int WindSpeed { get; set; }
}

public class WeatherAlertInfo
{
    public string Id { get; set; } = string.Empty;
    
    public string AlertType { get; set; } = string.Empty;
    
    public string Severity { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public DateTime StartTime { get; set; }
    
    public DateTime EndTime { get; set; }
    
    public string[] Areas { get; set; } = Array.Empty<string>();
}