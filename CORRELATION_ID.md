# Correlation ID Support

The MCPServerTM now includes comprehensive correlation ID support for end-to-end request tracing across the entire architecture: Postman → WeatherAPI → APIM (passthru) → MCPServer.

## Overview

Both the WeatherAPI and MCPServer now support correlation ID headers for distributed tracing. This enables you to track a single request across all services using the same Application Insights instance.

## Supported Headers

The services recognize and use these correlation ID headers (in order of precedence):

1. `x-correlation-id` - Custom correlation ID header
2. `Request-ID` - Microsoft/Azure standard header
3. `x-request-id` - Common HTTP standard header  
4. `x-ms-request-id` - Microsoft specific header

## How It Works

### Request Processing
1. **Header Detection**: Middleware checks for correlation ID in incoming request headers
2. **ID Generation**: If no correlation ID is found, a new GUID is generated
3. **Context Propagation**: Correlation ID is stored in HttpContext for use by services
4. **Response Headers**: Correlation ID is added to response headers for client visibility
5. **Telemetry Integration**: All Application Insights events include the correlation ID

### Service-to-Service Propagation
- **WeatherAPI → MCPServer**: The McpClientService automatically forwards correlation IDs when making requests to MCPServer
- **Middleware Coordination**: Both services use consistent correlation ID handling
- **Application Insights**: All telemetry events across services include the same correlation ID

## Usage Examples

### Client Requests with Correlation ID
```bash
# Using x-correlation-id header
curl -H "x-correlation-id: my-trace-123" http://localhost:5016/api/weather/Austin

# Using Request-ID header
curl -H "Request-ID: req-456" http://localhost:5270/mcp/info

# Without correlation ID (auto-generated)
curl http://localhost:5016/health
```

### Response Headers
All responses include the correlation ID in the `x-correlation-id` response header:
```
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8
x-correlation-id: my-trace-123
...
```

### Application Insights Integration

All telemetry events now include correlation ID properties:

#### WeatherAPI Events
- `Weather_Request` - Weather API requests
- `Weather_ApiRequest` - General API requests
- `Weather.Current.Requests` - Current weather metrics
- `Weather.Forecast.Requests` - Forecast weather metrics
- `Weather.Alerts.Requests` - Weather alerts metrics

#### MCPServer Events  
- `MCP_ToolUsage` - Tool execution tracking
- `MCP_ApiRequest` - API request patterns
- `MCP.Tool.Execution` - Individual tool metrics
- `MCP.Tools.List` - Tool listing metrics
- `MCP.Info.Access` - Server info access

### Application Insights Queries

Query correlated events across services:

```kql
// Find all events for a specific correlation ID
union customEvents, customMetrics
| where customDimensions.CorrelationId == "my-trace-123"
| project timestamp, name, customDimensions
| order by timestamp asc

// Track request flow across services
customEvents
| where customDimensions.CorrelationId == "my-trace-123"
| extend Service = case(
    name startswith "Weather_", "WeatherAPI",
    name startswith "MCP_", "MCPServer", 
    "Unknown")
| project timestamp, Service, name, customDimensions.Endpoint
| order by timestamp asc

// Performance analysis by correlation ID
customEvents
| where name in ("Weather_Request", "MCP_ToolUsage")
| where customDimensions.CorrelationId == "my-trace-123"  
| extend Duration = todouble(customMeasurements.Duration)
| project timestamp, name, Duration, customDimensions.CorrelationId
```

## Configuration

No additional configuration is required. The correlation ID support is automatically enabled when the services start.

## Testing

Use the included test script to validate correlation ID functionality:

```bash
# Make the test script executable
chmod +x test-correlation-id.sh

# Run the test
./test-correlation-id.sh
```

## Implementation Details

### WeatherAPI Changes
- `WeatherTelemetryMiddleware`: Handles correlation ID extraction, generation, and response headers
- `WeatherTelemetryService`: Includes correlation ID in all telemetry events
- `McpClientService`: Propagates correlation ID to MCPServer requests

### MCPServer Changes
- `TelemetryMiddleware`: Handles correlation ID extraction, generation, and response headers  
- `McpTelemetryService`: Includes correlation ID in all telemetry events

### Middleware Flow
1. Extract correlation ID from request headers
2. Generate new ID if none exists
3. Store in HttpContext.Items for service access
4. Add to response headers
5. Include in all telemetry events

This enables complete end-to-end tracing from external clients through APIM to both services, with all events correlated in Application Insights.