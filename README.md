# MCPServerTM

A Model Context Protocol (MCP) server implementation with OAuth 2.0 protection, providing weather tools through a REST API.

## Features

- **REST API** - Complete HTTP API with JWT Bearer authentication
- **OAuth 2.0 Protection** - Secure access with scope-based authorization
- **Weather Tools** - Three weather-related tools with realistic mock data
- **Swagger Documentation** - Interactive API documentation
- **Scope-based Access Control** - Requires `mcp:tools` scope for tool access

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

### Getting a Development Token

```bash
curl -X POST "http://localhost:5270/dev/token" \
  -H "Content-Type: application/json" \
  -d '{"userId": "test-user", "username": "Test User"}'
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
- `GET /.well-known/oauth-authorization-server` - OAuth metadata
- `POST /dev/token` - Development token generation (dev only)
- `GET /swagger` - API documentation

### Protected Endpoints (require authentication)
- `GET /mcp/tools` - List available tools
- `POST /mcp/tools/{toolName}` - Execute a specific tool (requires `mcp:tools` scope)

## Configuration

The server can be configured via `appsettings.json`:

```json
{
  "Jwt": {
    "Key": "your-256-bit-secret-key",
    "Issuer": "https://localhost:7000",
    "Audience": "mcp-server-api"
  }
}
```

For production with Entra ID, update the JWT configuration in `Program.cs` to use Azure AD authority and audience.

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

- JWT tokens are validated for signature, issuer, audience, and expiration
- Tools require the `mcp:tools` scope
- Development token endpoint is only available in Development environment
- HTTPS redirection is enabled

For production deployment:
- Use proper JWT signing keys
- Configure for your OAuth provider (e.g., Entra ID)
- Disable development token endpoint
- Use production-grade secrets management