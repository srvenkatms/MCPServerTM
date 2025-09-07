# MCPServerTM

A Model Context Protocol (MCP) server implementation with OAuth 2.0 protection, providing weather tools through a REST API.

## Features

- **REST API** - Complete HTTP API with JWT Bearer authentication
- **OAuth 2.0 Protection** - Secure access with scope-based authorization
- **Weather Tools** - Three weather-related tools with realistic mock data
- **Swagger Documentation** - Interactive API documentation
- **Scope-based Access Control** - Requires `mcp:tools` scope for tool access
- **Application Insights Integration** - Comprehensive monitoring and anomaly detection

## Architecture

The server implements the Model Context Protocol (MCP) with the following components:

- **Authentication**: JWT Bearer tokens with configurable issuer/audience
- **Authorization**: Scope-based access control (`mcp:tools` required)
- **Tools**: Weather-related tools with parameter validation
- **Discovery**: OAuth metadata endpoint for client configuration

## Weather Tools

1. **GetWeatherAlerts** - Get weather alerts for US states
2. **GetCurrentWeather** - Get current weather conditions with optional city
3. **GetWeatherForecast** - Get multi-day forecast (1-7 days)

## Quick Start

### Prerequisites
- .NET 8.0 SDK

### Running the Server

```bash
cd MCPServer
dotnet run
```

The server will start on `http://localhost:5270`

### Getting a Token from Entra ID

To use the MCP Server, you need to obtain a JWT token from your configured Entra ID tenant with the `mcp:tools` scope:

```bash
# Example token request to Entra ID
curl -X POST "https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id={client-id}" \
  -d "client_secret={client-secret}" \
  -d "scope=api://your-app-registration-id/mcp:tools"
```

### Using the Weather Tools

```bash
# Get your token first
TOKEN="your-jwt-token-here"

# List available tools
curl -X GET "http://localhost:5270/mcp/tools" \
  -H "Authorization: Bearer $TOKEN"

# Get weather alerts for California
curl -X POST "http://localhost:5270/mcp/tools/getweatheralerts" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"state": "CA"}'

# Get current weather for Austin, Texas
curl -X POST "http://localhost:5270/mcp/tools/getcurrentweather" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"state": "TX", "city": "Austin"}'

# Get 3-day forecast for New York
curl -X POST "http://localhost:5270/mcp/tools/getweatherforecast" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"state": "NY", "days": 3}'
```

## API Endpoints

### Public Endpoints
- `GET /health` - Health check
- `GET /mcp/info` - Server information and capabilities
- `GET /.well-known/oauth-authorization-server` - OAuth metadata (points to Entra ID)
- `GET /swagger` - API documentation

### Protected Endpoints (require Entra ID authentication)
- `GET /mcp/tools` - List available tools
- `POST /mcp/tools/{toolName}` - Execute a specific tool (requires `mcp:tools` scope)

## Configuration

The server is configured via `appsettings.json` for Entra ID integration:

```json
{
  "EntraId": {
    "Authority": "https://login.microsoftonline.com/{your-tenant-id}",
    "Audience": "api://your-app-registration-id"
  },
  "ApplicationInsights": {
    "ConnectionString": "your-application-insights-connection-string"
  }
}
```

Replace `{your-tenant-id}` with your actual Azure AD tenant ID and `{your-app-registration-id}` with your app registration ID.

### Application Insights Integration

The server includes comprehensive monitoring through Azure Application Insights:
- **Tool Usage Tracking**: Monitor which tools are being used and by whom
- **Authentication Monitoring**: Track authentication success/failure events
- **Performance Metrics**: Monitor API response times and system performance
- **Anomaly Detection**: Automatically detect unusual usage patterns
- **Custom Dashboards**: Create visualizations of usage patterns and system health

For detailed configuration and usage, see [APPLICATION_INSIGHTS.md](APPLICATION_INSIGHTS.md).

### Required Entra ID App Registration Setup

1. **Create an App Registration** in the Azure Portal
2. **Set API Permissions**:
   - Add a custom scope: `mcp:tools`
3. **Configure Token Claims**:
   - Ensure the `scope` claim includes `mcp:tools`
4. **App ID URI**: Set to `api://your-app-registration-id`

## Development

### Project Structure

```
MCPServer/
├── ModelContextProtocol/          # MCP framework implementation
│   ├── McpServerToolAttribute.cs  # Tool decoration
│   ├── McpServerToolTypeAttribute.cs
│   └── Server/
│       └── McpServerExtensions.cs # MCP server registration
├── Tools/
│   └── WeatherTools.cs           # Weather tool implementations
├── Program.cs                     # Main application configuration
└── appsettings.json              # Configuration
```

### Adding New Tools

1. Create a class with `[McpServerToolType]` attribute
2. Add methods with `[McpServerTool]` and `[Description]` attributes
3. Use `[Description]` on parameters for documentation
4. The framework automatically discovers and registers tools

Example:
```csharp
[McpServerToolType]
public class MyTools
{
    [McpServerTool, Description("Description of my tool")]
    public static async Task<object> MyTool(
        [Description("Parameter description")] string param1)
    {
        // Tool implementation
        return new { result = "success" };
    }
}
```

## Security

- JWT tokens are validated against Entra ID for signature, issuer, audience, and expiration
- Tools require the `mcp:tools` scope from your Entra ID app registration
- HTTPS redirection is enabled for production security

For production deployment:
- Configure your Entra ID tenant and app registration
- Set up proper API permissions and scopes
- Use production-grade secrets management
- Enable appropriate monitoring and logging