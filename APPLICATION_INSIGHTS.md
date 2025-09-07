# Application Insights Integration

This document describes the Application Insights integration added to the MCPServer for monitoring access patterns and detecting anomalies.

## Features

### 1. Core Application Insights Integration
- **Request Tracking**: All API requests are automatically tracked with performance metrics
- **Dependency Tracking**: External dependencies are monitored
- **Performance Counters**: System performance metrics collection
- **Event Counters**: .NET runtime metrics collection
- **Heartbeat**: Regular health checks sent to Application Insights

### 2. Custom MCP Telemetry Tracking
The `McpTelemetryService` provides specialized tracking for MCP operations:

#### Tool Usage Tracking
- Tracks every tool execution with parameters and performance metrics
- Records success/failure status and execution duration
- Captures user identity and client information
- Sanitizes sensitive parameters before logging

#### Authentication Event Tracking
- Monitors JWT token validation events
- Tracks authentication failures and successes
- Records user scopes and roles
- Identifies potential security issues

#### API Request Pattern Tracking
- Monitors all API endpoint usage
- Tracks response codes and performance
- Identifies usage patterns per user

#### Anomaly Detection
Built-in anomaly detection with configurable thresholds:
- **High Request Rate**: Detects users exceeding normal request patterns
- **High Failure Rate**: Identifies users with excessive failures
- **Real-time Monitoring**: Immediate alerting when thresholds are exceeded

### 3. Custom Events and Metrics
The following custom events are tracked:

#### Events
- `MCP_ToolUsage`: Tool execution with parameters and performance
- `MCP_Authentication`: Authentication events (success/failure)
- `MCP_ApiRequest`: General API request patterns
- `MCP_Anomaly`: Detected anomalies with details

#### Metrics
- `MCP.Tools.List`: Tool list endpoint access
- `MCP.Tool.Execution`: Individual tool execution metrics
- `MCP.Info.Access`: Server info endpoint access

## Configuration

### Application Insights Setup
Configure Application Insights in `appsettings.json`:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "your-application-insights-connection-string",
    "InstrumentationKey": null,
    "EnableRequestTrackingTelemetryModule": true,
    "EnableDependencyTrackingTelemetryModule": true,
    "EnablePerformanceCounterCollectionModule": true,
    "EnableEventCounterCollectionModule": true,
    "EnableHeartbeat": true,
    "CustomTelemetry": {
      "TrackToolUsage": true,
      "TrackAuthenticationEvents": true,
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
You can also configure Application Insights using environment variables (recommended for Azure deployments):

**Primary (Azure Container Apps standard):**
- `APPLICATIONINSIGHTS_CONNECTIONSTRING`: Connection string for Application Insights
- `APPLICATIONINSIGHTS_INSTRUMENTATIONKEY`: Legacy instrumentation key (deprecated)

**Fallback (Legacy naming):**
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: Connection string for Application Insights (with underscores)
- `APPLICATIONINSIGHTS_INSTRUMENTATION_KEY`: Legacy instrumentation key (with underscores, deprecated)

The application will automatically check for these environment variables in priority order:
1. Azure standard naming (without underscores) - used by Azure Container Apps
2. Legacy naming (with underscores) - for backward compatibility  
3. Configuration file settings

### Azure Configuration
When running in Azure App Service or Azure Container Apps, Application Insights can be configured through:
1. Azure Portal Application Insights resource
2. App Service/Container Apps Application Insights extension
3. Environment variables set by Azure (automatically uses the standard names above)
4. Manual environment variable configuration in Azure Container Apps or App Service

## Usage

### Viewing Telemetry Data
1. **Azure Portal**: Navigate to your Application Insights resource
2. **Live Metrics**: Real-time monitoring of requests and dependencies
3. **Analytics**: Query custom events and metrics using KQL
4. **Alerts**: Set up alerts based on custom metrics and events

### Example Analytics Queries

#### Tool Usage Analysis
```kql
customEvents
| where name == "MCP_ToolUsage"
| summarize count() by tostring(customDimensions.ToolName), bin(timestamp, 1h)
| render timechart
```

#### Authentication Failures
```kql
customEvents
| where name == "MCP_Authentication" and customDimensions.IsSuccess == "False"
| summarize count() by tostring(customDimensions.UserId), bin(timestamp, 1h)
```

#### Anomaly Detection
```kql
customEvents
| where name == "MCP_Anomaly"
| summarize count() by tostring(customDimensions.AnomalyType), bin(timestamp, 1h)
```

#### Performance Analysis
```kql
customEvents
| where name == "MCP_ToolUsage"
| extend Duration = todouble(customMeasurements.Duration)
| summarize avg(Duration), max(Duration), percentile(Duration, 95) by tostring(customDimensions.ToolName)
```

### Dashboards
Create custom dashboards in Azure Portal to visualize:
- Tool usage patterns
- Authentication success rates
- Performance metrics
- Anomaly alerts
- User activity patterns

## Security Considerations

### Data Privacy
- User identifiers are captured but personal information is excluded
- Tool parameters are sanitized before logging
- Sensitive configuration values are not logged

### Anomaly Thresholds
Configure appropriate thresholds based on your usage patterns:
- `MaxRequestsPerMinute`: Adjust based on expected user activity
- `MaxFailuresPerMinute`: Set based on acceptable failure rates

## Troubleshooting

### No Data in Application Insights
1. Verify connection string is correctly configured
2. Check Azure subscription and resource group permissions
3. Ensure Application Insights resource is active
4. Verify network connectivity from deployment environment

### Azure Container Apps Configuration
If you're not seeing telemetry data in Azure Container Apps:
1. **Check Environment Variables**: The application supports both naming conventions:
   - **Azure standard** (preferred): `APPLICATIONINSIGHTS_CONNECTIONSTRING`, `APPLICATIONINSIGHTS_INSTRUMENTATIONKEY`  
   - **Legacy naming**: `APPLICATIONINSIGHTS_CONNECTION_STRING`, `APPLICATIONINSIGHTS_INSTRUMENTATION_KEY`
2. **Verify Connection String Format**: The connection string should follow this format:
   ```
   InstrumentationKey=your-key;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/;LiveEndpoint=https://your-region.livediagnostics.monitor.azure.com/
   ```
3. **Container Apps Settings**: In Azure Container Apps, set the environment variable in the "Environment variables" section of your container app configuration
4. **Check Application Logs**: Review the container logs for any Application Insights initialization errors

### Missing Custom Events
1. Check configuration flags in `appsettings.json`
2. Verify `McpTelemetryService` is properly registered
3. Check application logs for any errors

### High Volume Costs
1. Adjust sampling rates in Application Insights configuration
2. Reduce tracked events frequency
3. Implement intelligent sampling based on usage patterns

## Benefits for MCP Server Monitoring

1. **Operational Insights**: Understand how tools are being used
2. **Performance Optimization**: Identify slow operations and optimize
3. **Security Monitoring**: Detect unusual access patterns
4. **User Behavior**: Analyze usage patterns for improvements
5. **Proactive Alerting**: Get notified of issues before they impact users
6. **Compliance**: Track access for audit and compliance requirements