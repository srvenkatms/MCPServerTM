#!/bin/bash

# Test script for MCP Server API
# Make sure the server is running on http://localhost:5270
# NOTE: This server now requires Entra ID tokens instead of development tokens

BASE_URL="http://localhost:5270"

echo "🌤️  MCP Weather Server API Test Script"
echo "======================================"
echo "⚠️  Note: Server now requires Entra ID authentication"
echo ""

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
echo "🔐 Testing OAuth Metadata Endpoint (now points to Entra ID)..."
curl -s "$BASE_URL/.well-known/oauth-authorization-server" | jq . || echo "❌ OAuth metadata failed"

# Test that development token endpoint is removed
echo ""
echo "🚫 Testing Development Token Endpoint (should be 404)..."
RESPONSE=$(curl -s -w "%{http_code}" -o /dev/null "$BASE_URL/dev/token")
if [ "$RESPONSE" = "404" ]; then
    echo "✅ Development token endpoint correctly removed (404)"
else
    echo "❌ Expected 404, got $RESPONSE"
fi

# Test tools endpoint - should now work without token
echo ""
echo "🔧 Testing MCP Tools Endpoint (should work without authentication)..."
RESPONSE=$(curl -s -w "%{http_code}" -o /dev/null "$BASE_URL/mcp/tools")
if [ "$RESPONSE" = "200" ]; then
    echo "✅ Tools endpoint accessible without authentication (200)"
else
    echo "❌ Expected 200, got $RESPONSE"
fi

echo ""
echo "🎉 Basic endpoint tests completed!"
echo ""
echo "💡 To view the interactive API documentation, visit:"
echo "   $BASE_URL/swagger"
echo ""
echo "🔑 To test the protected endpoints, you need a valid Entra ID token:"
echo "   curl -X POST 'https://login.microsoftonline.com/16b3c013-d300-468d-ac64-7eda0820b6d3/oauth2/v2.0/token' \\"
echo "     -H 'Content-Type: application/x-www-form-urlencoded' \\"
echo "     -d 'grant_type=client_credentials' \\"
echo "     -d 'client_id=f46c8d97-187b-4020-954c-04a831be0a74' \\"
echo "     -d 'client_secret={client-secret}' \\"
echo "     -d 'scope=api://f46c8d97-187b-4020-954c-04a831be0a74/mcp:tools'"