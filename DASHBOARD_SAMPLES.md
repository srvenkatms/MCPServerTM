# Sample Application Insights Dashboard Configuration

This document provides sample KQL queries and dashboard configurations for monitoring the MCP Server through Application Insights.

## Dashboard Tiles

### 1. Tool Usage Overview
```kql
customEvents
| where name == "MCP_ToolUsage"
| where timestamp > ago(24h)
| summarize RequestCount = count() by tostring(customDimensions.ToolName)
| render piechart
```

### 2. Authentication Success Rate
```kql
customEvents
| where name == "MCP_Authentication"
| where timestamp > ago(24h)
| summarize 
    Total = count(),
    Successful = countif(customDimensions.IsSuccess == "True"),
    Failed = countif(customDimensions.IsSuccess == "False")
| extend SuccessRate = round(100.0 * Successful / Total, 2)
| project SuccessRate, Successful, Failed, Total
```

### 3. Tool Performance Metrics
```kql
customEvents
| where name == "MCP_ToolUsage"
| where timestamp > ago(24h)
| extend Duration = todouble(customMeasurements.Duration)
| where isnotnull(Duration)
| summarize 
    AvgDuration = round(avg(Duration), 2),
    MaxDuration = round(max(Duration), 2),
    P95Duration = round(percentile(Duration, 95), 2),
    RequestCount = count()
    by tostring(customDimensions.ToolName)
| order by AvgDuration desc
```

### 4. Anomaly Detection Timeline
```kql
customEvents
| where name == "MCP_Anomaly"
| where timestamp > ago(7d)
| summarize AnomalyCount = count() by 
    tostring(customDimensions.AnomalyType),
    bin(timestamp, 1h)
| render timechart
```

### 5. User Activity Patterns
```kql
customEvents
| where name == "MCP_ToolUsage"
| where timestamp > ago(7d)
| summarize RequestCount = count() by 
    tostring(customDimensions.UserId),
    bin(timestamp, 1d)
| render timechart
```

### 6. API Response Codes
```kql
customEvents
| where name == "MCP_ApiRequest"
| where timestamp > ago(24h)
| summarize RequestCount = count() by tostring(customDimensions.StatusCode)
| render barchart
```

### 7. Failed Requests by Endpoint
```kql
customEvents
| where name == "MCP_ApiRequest"
| where customDimensions.StatusCode !in ("200", "201", "202", "204")
| where timestamp > ago(24h)
| summarize FailureCount = count() by tostring(customDimensions.Endpoint)
| order by FailureCount desc
```

## Alert Rules

### 1. High Failure Rate Alert
```kql
customEvents
| where name == "MCP_ToolUsage"
| where timestamp > ago(10m)
| summarize 
    Total = count(),
    Failures = countif(customDimensions.IsSuccess == "False")
| extend FailureRate = (100.0 * Failures / Total)
| where FailureRate > 10
```

### 2. Anomaly Detection Alert
```kql
customEvents
| where name == "MCP_Anomaly"
| where timestamp > ago(5m)
| summarize AnomalyCount = count()
| where AnomalyCount > 0
```

### 3. Performance Degradation Alert
```kql
customEvents
| where name == "MCP_ToolUsage"
| where timestamp > ago(15m)
| extend Duration = todouble(customMeasurements.Duration)
| where isnotnull(Duration)
| summarize AvgDuration = avg(Duration)
| where AvgDuration > 5000 // Alert if average > 5 seconds
```

## Custom Metrics for Monitoring

### 1. Tool Execution Rate
```kql
customEvents
| where name == "MCP_ToolUsage"
| summarize count() by bin(timestamp, 1m)
| render timechart
```

### 2. Unique Users Over Time
```kql
customEvents
| where name == "MCP_ToolUsage"
| summarize UniqueUsers = dcount(tostring(customDimensions.UserId)) by bin(timestamp, 1h)
| render timechart
```

### 3. Request Distribution by Tool
```kql
customEvents
| where name == "MCP_ToolUsage"
| where timestamp > ago(24h)
| make-series RequestCount = count() default = 0 on timestamp step 1h by tostring(customDimensions.ToolName)
| render timechart
```

## Workbook Template

Create a comprehensive monitoring workbook with the following sections:

1. **Executive Summary**
   - Total requests in the last 24h
   - Success rate
   - Average response time
   - Unique users

2. **Tool Analytics**
   - Most used tools
   - Performance by tool
   - Usage patterns over time

3. **Security Monitoring**
   - Authentication events
   - Failed access attempts
   - Anomaly detections

4. **Performance Analysis**
   - Response time trends
   - Slowest operations
   - Error rate by endpoint

5. **User Behavior**
   - Active users
   - Usage patterns
   - Geographic distribution (if available)

## Live Metrics Stream

Monitor real-time activity using Live Metrics Stream with custom filters:
- Filter by `customEvents` with name starting with "MCP_"
- Monitor custom metrics for real-time anomaly detection
- Track performance counters for system health

## Sample PowerShell Dashboard Setup

```powershell
# Example PowerShell script to create dashboard programmatically
$resourceGroup = "your-resource-group"
$dashboardName = "MCP-Server-Dashboard"
$subscriptionId = "your-subscription-id"

# Dashboard JSON configuration would go here
# This is a simplified example - full implementation would include all tiles
```

This dashboard configuration provides comprehensive monitoring capabilities for the MCP Server, enabling proactive monitoring and quick identification of issues or unusual patterns.