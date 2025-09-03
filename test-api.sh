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

# Test authentication - should fail without token
echo ""
echo "🚫 Testing Unauthorized Access (should fail)..."
RESPONSE=$(curl -s -w "%{http_code}" -o /dev/null "$BASE_URL/mcp/tools")
if [ "$RESPONSE" = "401" ]; then
    echo "✅ Correctly blocked unauthorized access (401)"
else
    echo "❌ Expected 401, got $RESPONSE"
fi

echo ""
echo "🎉 Basic endpoint tests completed!"
echo ""
echo "💡 To view the interactive API documentation, visit:"
echo "   $BASE_URL/swagger"
echo ""
echo "🔑 To test the protected endpoints, you need a valid Entra ID token:"
echo "   curl -X POST 'https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token' \\"
echo "     -H 'Content-Type: application/x-www-form-urlencoded' \\"
echo "     -d 'grant_type=client_credentials' \\"
echo "     -d 'client_id={client-id}' \\"
echo "     -d 'client_secret={client-secret}' \\"
echo "     -d 'scope=api://your-app-registration-id/mcp:tools'"