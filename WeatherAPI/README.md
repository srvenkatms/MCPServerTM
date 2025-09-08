# Weather API

A comprehensive Weather API that integrates with Agent Foundry SDK and MCP (Model Context Protocol) Server to provide weather information for cities across the United States.

## Features

- **City-based Weather Information**: Get weather data for any US city
- **Agent Foundry Integration**: Uses Agent Foundry SDK for LLM-powered agent management
- **MCP Client**: Connects to MCP Server to retrieve weather data using weather tools
- **Multiple Endpoints**: Current weather, forecasts, alerts, and comprehensive weather data
- **Application Insights Integration**: Comprehensive telemetry and monitoring for operations and performance
- **Configurable**: All settings externalized to configuration files
- **Development Ready**: Includes development authentication for testing

## API Endpoints

### Weather Information
- `GET /api/weather/{city}?state={state}&days={days}` - Complete weather information
- `POST /api/weather` - Complete weather information via JSON request
- `GET /api/weather/{city}/current` - Current weather conditions only
- `GET /api/weather/{city}/forecast?days={days}` - Weather forecast only
- `GET /api/weather/{city}/alerts` - Weather alerts only

### System Endpoints
- `GET /health` - API health check
- `GET /swagger` - API documentation (in development)

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- MCP Server running (included in this repository)

### Running the API

1. **Start the MCP Server** (in separate terminal):
   ```bash
   cd MCPServer
   dotnet run
   ```
   The MCP Server will start on `http://localhost:5270`

2. **Start the Weather API**:
   ```bash
   cd WeatherAPI/WeatherAPI
   dotnet run
   ```
   The Weather API will start on `http://localhost:5016`

3. **Test the API**:
   ```bash
   curl "http://localhost:5016/api/weather/Austin?state=TX"
   ```

## Example Usage

### Get Complete Weather Information
```bash
curl "http://localhost:5016/api/weather/Seattle?state=WA&days=3"
```

### Get Current Weather Only
```bash
curl "http://localhost:5016/api/weather/Miami/current"
```

### Using POST Method
```bash
curl -X POST "http://localhost:5016/api/weather" \
  -H "Content-Type: application/json" \
  -d '{
    "city": "Chicago",
    "state": "IL", 
    "days": 5
  }'
```

## Sample Response

```json
{
  "city": "Austin",
  "state": "TX",
  "currentWeather": {
    "location": "Austin, Texas",
    "temperature": {
      "current": 78,
      "unit": "°F",
      "feelsLike": 68
    },
    "conditions": {
      "description": "Thunderstorms",
      "humidity": 76,
      "windSpeed": 23,
      "windDirection": "NW",
      "visibility": 9
    },
    "timestamp": "2025-09-04T16:27:37+00:00"
  },
  "forecast": [
    {
      "date": "2025-09-05",
      "dayOfWeek": "Friday",
      "temperature": {
        "high": 80,
        "low": 57,
        "unit": "°F"
      },
      "conditions": "Sunny",
      "precipitationChance": 5,
      "windSpeed": 17
    }
  ],
  "alerts": [
    {
      "alertType": "Heat Advisory",
      "severity": "Moderate",
      "description": "Excessive heat warning for Texas. Temperatures may reach above 95°F.",
      "areas": ["Metro Area", "Central Region"]
    }
  ],
  "agentId": "agent-WeatherAgent-56e1d4fd",
  "retrievedAt": "2025-09-04T16:27:37.823094Z"
}
```

## Configuration

All configurable elements are in `appsettings.json`:

### Agent Foundry Configuration
```json
{
  "AgentFoundry": {
    "Endpoint": "https://your-ai-foundry-endpoint.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "DefaultAgentName": "WeatherAgent",
    "ModelDeploymentName": "gpt-4"
  }
}
```

### MCP Server Configuration
```json
{
  "McpServer": {
    "BaseUrl": "http://localhost:5270",
    "AuthenticationRequired": true,
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TokenEndpoint": "https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token",
    "TenantId": "{tenant-id}",
    "Scope": "api://your-app-registration/.default"
  }
}
```

**Note**: For Azure AD OAuth2 authentication, use `/.default` scope instead of `/mcp:tools` to ensure proper token acquisition.

### Weather Prompt Templates
```json
{
  "WeatherPrompts": {
    "CurrentWeatherTemplate": "Get the current weather information for the city: {city}. Include temperature, conditions, humidity, and wind information.",
    "ForecastTemplate": "Get the weather forecast for {city} for the next {days} days. Include daily high/low temperatures and conditions.",
    "AlertsTemplate": "Get any weather alerts or warnings for the area around {city}. Include severity and description of any active alerts."
  }
}
```

## Application Insights Integration

The Weather API includes comprehensive Application Insights integration for monitoring and telemetry, similar to the MCPServer implementation.

### Features
- **Request Tracking**: All API requests are automatically tracked with performance metrics
- **Weather-Specific Telemetry**: Custom events for weather operations (current weather, forecasts, alerts)
- **Dependency Tracking**: External dependencies (Agent Foundry, MCP Server) are monitored
- **Anomaly Detection**: Built-in detection for unusual request patterns
- **Performance Monitoring**: Response times and throughput metrics

### Configuration

Add Application Insights configuration to `appsettings.json`:

```json
{
  "ApplicationInsights": {
    "ConnectionString": null,
    "InstrumentationKey": null,
    "EnableRequestTrackingTelemetryModule": true,
    "EnableDependencyTrackingTelemetryModule": true,
    "EnablePerformanceCounterCollectionModule": true,
    "EnableEventCounterCollectionModule": true,
    "EnableHeartbeat": true,
    "CustomTelemetry": {
      "TrackWeatherRequests": true,
      "TrackPerformanceMetrics": true,
      "TrackAnomalies": true,
      "AnomalyDetection": {
        "MaxRequestsPerMinute": 100,
        "MaxFailuresPerMinute": 10
      }
    }
  }
}
```

### Environment Variables

For production deployments, configure Application Insights using environment variables:

```bash
# Primary (Azure Container Apps standard)
export APPLICATIONINSIGHTS_CONNECTIONSTRING="your-application-insights-connection-string"

# Legacy (backward compatibility)
export APPLICATIONINSIGHTS_CONNECTION_STRING="your-application-insights-connection-string"
```

### Custom Events Tracked

The Weather API tracks the following custom events:
- `Weather_Request`: Weather requests with city, operation type, and performance data
- `Weather_Operation`: Specific weather operations (current, forecast, alerts)
- `Weather_ApiRequest`: General API request patterns
- `Weather_Anomaly`: Detected anomalies with details

### Custom Metrics

- `Weather.Current.Requests`: Current weather endpoint access
- `Weather.Forecast.Requests`: Forecast endpoint access  
- `Weather.Alerts.Requests`: Alerts endpoint access
- `Weather.General.Requests`: General weather endpoint access

### Viewing Telemetry Data

1. **Azure Portal**: Navigate to your Application Insights resource
2. **Live Metrics**: Real-time monitoring of weather requests
3. **Analytics**: Query custom events using KQL:

```kql
// Weather requests by city
customEvents
| where name == "Weather_Request"
| summarize count() by tostring(customDimensions.City), bin(timestamp, 1h)
| render timechart

// Performance analysis by operation type
customEvents  
| where name == "Weather_Operation"
| extend Duration = todouble(customMeasurements.Duration)
| summarize avg(Duration), max(Duration), percentile(Duration, 95) by tostring(customDimensions.OperationType)
```

## Development

The Weather API is built with the following components:

1. **Controllers**: Handle HTTP requests and responses
2. **Services**: 
   - `AgentFoundryService`: Manages AI agents using Agent Foundry SDK
   - `McpClientService`: Communicates with MCP Server weather tools
   - `WeatherService`: Orchestrates weather data retrieval
3. **Models**: Define data structures for requests, responses, and configuration
4. **Configuration**: Externalized settings for all components

## Development

For development, the API uses simplified authentication with the MCP Server. The MCP Server includes a development token endpoint (`/dev/token`) that provides mock authentication for testing.

## Architecture

The Weather API is built with the following components:

1. **Controllers**: Handle HTTP requests and responses
2. **Services**: 
   - `AgentFoundryService`: Manages AI agents using Agent Foundry SDK
   - `McpClientService`: Communicates with MCP Server weather tools
   - `WeatherService`: Orchestrates weather data retrieval
   - `WeatherTelemetryService`: Handles Application Insights telemetry tracking
3. **Middleware**:
   - `WeatherTelemetryMiddleware`: Tracks API requests and performance metrics
4. **Models**: Define data structures for requests, responses, and configuration
5. **Configuration**: Externalized settings for all components

## Production Deployment

For production deployment:

1. Configure Agent Foundry with your Azure AI Foundry endpoint and API key
2. Set up proper MCP Server authentication with Entra ID
3. Update configuration files with production settings
4. Use secure secrets management for API keys and connection strings

### Environment Variable Overrides

For production deployments, you can override configuration values using environment variables. Use double underscores (`__`) to represent nested configuration sections:

```bash
# MCP Server Configuration
export McpServer__BaseUrl="https://your-mcp-server.azurecontainerapps.io"
export McpServer__ClientId="your-client-id"
export McpServer__ClientSecret="your-client-secret"
export McpServer__TenantId="your-tenant-id"
export McpServer__Scope="api://your-app-registration/.default"

# Agent Foundry Configuration  
export AgentFoundry__Endpoint="https://your-ai-foundry-endpoint.openai.azure.com/"
export AgentFoundry__ApiKey="your-api-key"
```

This approach allows you to keep sensitive values out of configuration files while maintaining flexibility for different deployment environments.

## Error Handling

The API includes comprehensive error handling:
- Invalid city names return appropriate error messages
- MCP Server connection failures are gracefully handled
- Authentication failures provide clear error responses
- All errors are logged for debugging

## Support

For questions or issues, please refer to the main repository documentation or create an issue in the GitHub repository.