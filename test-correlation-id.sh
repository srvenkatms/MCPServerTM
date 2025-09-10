#!/bin/bash

# Test script for correlation ID end-to-end tracing
# Tests: Postman -> WeatherAPI -> APIM (passthru) -> MCPServer

echo "Testing Correlation ID end-to-end tracing..."
echo "=========================================="

# Base URLs (adjust as needed for your environment)
WEATHER_API_BASE_URL="http://localhost:5016"
MCP_SERVER_BASE_URL="http://localhost:5270"

# Test correlation ID to use
TEST_CORRELATION_ID="test-$(date +%s)-$(uuidgen | head -c 8)"

echo "Using test correlation ID: $TEST_CORRELATION_ID"
echo ""

# Function to extract correlation ID from response headers
extract_correlation_id() {
    grep -i "x-correlation-id:" | sed 's/.*x-correlation-id: *//I' | tr -d '\r'
}

echo "1. Testing WeatherAPI correlation ID handling..."
echo "==============================================="

echo "Testing health endpoint with x-correlation-id header..."
echo "Request: curl -H \"x-correlation-id: $TEST_CORRELATION_ID\" $WEATHER_API_BASE_URL/health"
RESPONSE=$(curl -i -H "x-correlation-id: $TEST_CORRELATION_ID" "$WEATHER_API_BASE_URL/health" 2>/dev/null)
RETURNED_ID=$(echo "$RESPONSE" | extract_correlation_id)
echo "Response correlation ID: $RETURNED_ID"
if [[ "$RETURNED_ID" == "$TEST_CORRELATION_ID" ]]; then
    echo "✅ Correlation ID correctly preserved"
else
    echo "❌ Correlation ID mismatch. Expected: $TEST_CORRELATION_ID, Got: $RETURNED_ID"
fi
echo ""

echo "Testing health endpoint without correlation ID (should generate one)..."
echo "Request: curl $WEATHER_API_BASE_URL/health"
RESPONSE=$(curl -i "$WEATHER_API_BASE_URL/health" 2>/dev/null)
GENERATED_ID=$(echo "$RESPONSE" | extract_correlation_id)
echo "Generated correlation ID: $GENERATED_ID"
if [[ -n "$GENERATED_ID" && "$GENERATED_ID" != "null" ]]; then
    echo "✅ Correlation ID correctly generated"
else
    echo "❌ Failed to generate correlation ID"
fi
echo ""

echo "Testing with Request-ID header..."
REQUEST_ID="${TEST_CORRELATION_ID}-request-id"
echo "Request: curl -H \"Request-ID: $REQUEST_ID\" $WEATHER_API_BASE_URL/health"
RESPONSE=$(curl -i -H "Request-ID: $REQUEST_ID" "$WEATHER_API_BASE_URL/health" 2>/dev/null)
RETURNED_ID=$(echo "$RESPONSE" | extract_correlation_id)
echo "Response correlation ID: $RETURNED_ID"
if [[ "$RETURNED_ID" == "$REQUEST_ID" ]]; then
    echo "✅ Request-ID header correctly handled"
else
    echo "❌ Request-ID header not handled correctly. Expected: $REQUEST_ID, Got: $RETURNED_ID"
fi
echo ""

echo "2. Testing MCPServer correlation ID handling..."
echo "==============================================="

echo "Testing health endpoint with x-correlation-id header..."
echo "Request: curl -H \"x-correlation-id: $TEST_CORRELATION_ID-mcp\" $MCP_SERVER_BASE_URL/health"
RESPONSE=$(curl -i -H "x-correlation-id: $TEST_CORRELATION_ID-mcp" "$MCP_SERVER_BASE_URL/health" 2>/dev/null)
RETURNED_ID=$(echo "$RESPONSE" | extract_correlation_id)
echo "Response correlation ID: $RETURNED_ID"
if [[ "$RETURNED_ID" == "$TEST_CORRELATION_ID-mcp" ]]; then
    echo "✅ MCPServer correlation ID correctly preserved"
else
    echo "❌ MCPServer correlation ID mismatch. Expected: $TEST_CORRELATION_ID-mcp, Got: $RETURNED_ID"
fi
echo ""

echo "Testing MCP info endpoint without correlation ID..."
echo "Request: curl $MCP_SERVER_BASE_URL/mcp/info"
RESPONSE=$(curl -i "$MCP_SERVER_BASE_URL/mcp/info" 2>/dev/null)
GENERATED_ID=$(echo "$RESPONSE" | extract_correlation_id)
echo "Generated correlation ID: $GENERATED_ID"
if [[ -n "$GENERATED_ID" && "$GENERATED_ID" != "null" ]]; then
    echo "✅ MCPServer correlation ID correctly generated"
else
    echo "❌ MCPServer failed to generate correlation ID"
fi
echo ""

echo "3. Testing different header formats..."
echo "====================================="

# Test different header names
HEADERS=("x-correlation-id" "Request-ID" "x-request-id" "x-ms-request-id")
for header in "${HEADERS[@]}"; do
    test_id="${TEST_CORRELATION_ID}-${header//[^a-zA-Z]/}"
    echo "Testing with $header header..."
    echo "Request: curl -H \"$header: $test_id\" $WEATHER_API_BASE_URL/health"
    
    RESPONSE=$(curl -i -H "$header: $test_id" "$WEATHER_API_BASE_URL/health" 2>/dev/null)
    RETURNED_ID=$(echo "$RESPONSE" | extract_correlation_id)
    
    echo "Response correlation ID: $RETURNED_ID"
    if [[ "$RETURNED_ID" == "$test_id" ]]; then
        echo "✅ $header header correctly handled"
    else
        echo "❌ $header header not handled correctly. Expected: $test_id, Got: $RETURNED_ID"
    fi
    echo ""
done

echo "4. Summary and Manual Testing Instructions..."
echo "============================================="

echo "✅ Automated tests completed!"
echo ""
echo "For manual end-to-end testing with both services running:"
echo ""
echo "1. Start MCPServer:"
echo "   cd MCPServer && dotnet run"
echo ""
echo "2. Start WeatherAPI (in another terminal):"
echo "   cd WeatherAPI/WeatherAPI && dotnet run"
echo ""
echo "3. Test correlation ID propagation:"
echo "   curl -H 'x-correlation-id: end-to-end-test' 'http://localhost:5016/api/weather/Austin'"
echo ""
echo "4. Check Application Insights for events with CorrelationId property:"
echo "   - Search for events with customDimensions.CorrelationId == 'end-to-end-test'"
echo "   - Both Weather_* and MCP_* events should have the same correlation ID"
echo ""

echo "Expected behavior:"
echo "- ✅ WeatherAPI receives request with x-correlation-id header"
echo "- ✅ WeatherAPI returns same correlation ID in response header"
echo "- ✅ WeatherAPI includes correlation ID in Application Insights telemetry"
echo "- ✅ WeatherAPI forwards correlation ID when calling MCPServer"
echo "- ✅ MCPServer receives and preserves correlation ID"
echo "- ✅ MCPServer includes correlation ID in Application Insights telemetry"
echo "- ✅ End-to-end traceability through Application Insights"

echo ""
echo "Application Insights Query Example:"
echo "union customEvents, customMetrics"
echo "| where customDimensions.CorrelationId == 'end-to-end-test'"
echo "| project timestamp, name, customDimensions"
echo "| order by timestamp asc"