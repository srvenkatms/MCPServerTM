#!/bin/bash

# Application Insights Integration Verification Script
# This script verifies that the Application Insights integration is properly configured

echo "🔍 Application Insights Integration Verification"
echo "=============================================="
echo ""

# Check if Application Insights packages are installed
echo "📦 Checking NuGet packages..."
cd "$(dirname "$0")/MCPServer"

if dotnet list package | grep -q "Microsoft.ApplicationInsights.AspNetCore"; then
    echo "✅ Microsoft.ApplicationInsights.AspNetCore package found"
else
    echo "❌ Microsoft.ApplicationInsights.AspNetCore package missing"
fi

if dotnet list package | grep -q "Microsoft.ApplicationInsights.PerfCounterCollector"; then
    echo "✅ Microsoft.ApplicationInsights.PerfCounterCollector package found"
else
    echo "❌ Microsoft.ApplicationInsights.PerfCounterCollector package missing"
fi

echo ""

# Check if project builds successfully
echo "🔨 Checking build status..."
if dotnet build --verbosity quiet > /dev/null 2>&1; then
    echo "✅ Project builds successfully"
else
    echo "❌ Build failed"
    echo "Run 'dotnet build' for details"
fi

echo ""

# Check for Application Insights configuration
echo "⚙️ Checking configuration files..."
if [ -f "appsettings.json" ] && grep -q "ApplicationInsights" appsettings.json; then
    echo "✅ Application Insights configuration found in appsettings.json"
else
    echo "❌ Application Insights configuration missing from appsettings.json"
fi

if [ -f "appsettings.Sample.json" ]; then
    echo "✅ Sample configuration file available"
else
    echo "❌ Sample configuration file missing"
fi

echo ""

# Check for custom services
echo "🏗️ Checking custom services..."
if [ -f "Services/McpTelemetryService.cs" ]; then
    echo "✅ McpTelemetryService found"
else
    echo "❌ McpTelemetryService missing"
fi

if [ -f "Middleware/TelemetryMiddleware.cs" ]; then
    echo "✅ TelemetryMiddleware found"
else
    echo "❌ TelemetryMiddleware missing"
fi

echo ""

# Check Program.cs for Application Insights integration
echo "🔧 Checking Program.cs integration..."
if grep -q "AddApplicationInsightsTelemetry" Program.cs; then
    echo "✅ Application Insights telemetry service registration found"
else
    echo "❌ Application Insights telemetry service registration missing"
fi

if grep -q "McpTelemetryService" Program.cs; then
    echo "✅ Custom MCP telemetry service registration found"
else
    echo "❌ Custom MCP telemetry service registration missing"
fi

if grep -q "TelemetryMiddleware" Program.cs; then
    echo "✅ Telemetry middleware registration found"
else
    echo "❌ Telemetry middleware registration missing"
fi

echo ""

# Check documentation
echo "📚 Checking documentation..."
if [ -f "../APPLICATION_INSIGHTS.md" ]; then
    echo "✅ Application Insights documentation found"
else
    echo "❌ Application Insights documentation missing"
fi

if grep -q "Application Insights" ../README.md; then
    echo "✅ README.md updated with Application Insights information"
else
    echo "❌ README.md missing Application Insights information"
fi

echo ""

# Summary
echo "📋 Summary:"
echo "The Application Insights integration includes:"
echo "   • Custom telemetry service for MCP-specific events"
echo "   • Middleware for request tracking and performance monitoring"
echo "   • Authentication event tracking"
echo "   • Anomaly detection with configurable thresholds"
echo "   • Tool usage monitoring with parameters and metrics"
echo ""

echo "🚀 Next Steps:"
echo "   1. Configure your Application Insights connection string in appsettings.json"
echo "   2. Deploy to Azure and verify telemetry data appears in Application Insights"
echo "   3. Set up custom dashboards and alerts based on your monitoring requirements"
echo "   4. Review APPLICATION_INSIGHTS.md for detailed configuration options"
echo ""

echo "✨ Integration verification complete!"