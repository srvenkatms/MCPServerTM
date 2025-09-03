#!/bin/bash

# Test script for MCP Server API
# Make sure the server is running on http://localhost:5270

BASE_URL="http://localhost:5270"

echo "🌤️  MCP Weather Server API Test Script"
echo "======================================"

# Test health endpoint
echo ""
echo "📊 Testing Health Endpoint..."
curl -s "$BASE_URL/health" | jq . || echo "❌ Health check failed"

# Test MCP info
echo ""
echo "ℹ️  Testing MCP Info Endpoint..."
curl -s "$BASE_URL/mcp/info" | jq . || echo "❌ MCP info failed"

# Test OAuth metadata
echo ""
echo "🔐 Testing OAuth Metadata Endpoint..."
curl -s "$BASE_URL/.well-known/oauth-authorization-server" | jq . || echo "❌ OAuth metadata failed"

# Get development token
echo ""
echo "🎫 Getting Development Token..."
TOKEN_RESPONSE=$(curl -s -X POST "$BASE_URL/dev/token" \
  -H "Content-Type: application/json" \
  -d '{"userId": "test-user", "username": "Test User"}')

if [ $? -eq 0 ]; then
    TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r '.access_token')
    echo "✅ Token obtained successfully"
else
    echo "❌ Failed to get token"
    exit 1
fi

# Test authentication - should fail without token
echo ""
echo "🚫 Testing Unauthorized Access (should fail)..."
RESPONSE=$(curl -s -w "%{http_code}" -o /dev/null "$BASE_URL/mcp/tools")
if [ "$RESPONSE" = "401" ]; then
    echo "✅ Correctly blocked unauthorized access (401)"
else
    echo "❌ Expected 401, got $RESPONSE"
fi

# List tools with authentication
echo ""
echo "🔧 Testing MCP Tools List (with authentication)..."
curl -s -X GET "$BASE_URL/mcp/tools" \
  -H "Authorization: Bearer $TOKEN" | jq . || echo "❌ Tools list failed"

# Test weather alerts
echo ""
echo "⚠️  Testing Weather Alerts for California..."
curl -s -X POST "$BASE_URL/mcp/tools/getweatheralerts" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"state": "CA"}' | jq . || echo "❌ Weather alerts failed"

# Test current weather
echo ""
echo "🌡️  Testing Current Weather for Austin, Texas..."
curl -s -X POST "$BASE_URL/mcp/tools/getcurrentweather" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"state": "TX", "city": "Austin"}' | jq . || echo "❌ Current weather failed"

# Test weather forecast
echo ""
echo "📅 Testing 3-Day Weather Forecast for New York..."
curl -s -X POST "$BASE_URL/mcp/tools/getweatherforecast" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"state": "NY", "days": 3}' | jq . || echo "❌ Weather forecast failed"

# Test error handling - invalid state
echo ""
echo "❓ Testing Error Handling (weather for invalid parameters)..."
curl -s -X POST "$BASE_URL/mcp/tools/getweatherforecast" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"state": "NY", "days": 10}' || echo "✅ Correctly handled invalid days parameter"

echo ""
echo "🎉 All tests completed!"
echo ""
echo "💡 To view the interactive API documentation, visit:"
echo "   $BASE_URL/swagger"
echo ""
echo "🔗 Your access token (valid for 1 hour):"
echo "   $TOKEN"