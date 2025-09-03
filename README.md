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

### Getting a Token from Entra ID

To use the MCP Server, you need to obtain a JWT token from your configured Entra ID tenant with the `mcp:tools` scope. The MCP Server uses the OAuth 2.0 Client Credentials flow for service-to-service authentication.

#### Prerequisites
- Completed App Registration setup (see "Required Entra ID App Registration Setup" section)
- Client ID and Client Secret from your app registration
- Your tenant ID

#### Token Request Example

```bash
# Replace with your actual values
TENANT_ID="your-tenant-id"
CLIENT_ID="your-client-id"  
CLIENT_SECRET="your-client-secret"
APP_REGISTRATION_ID="your-app-registration-id"

# Request token from Entra ID
curl -X POST "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=${CLIENT_ID}" \
  -d "client_secret=${CLIENT_SECRET}" \
  -d "scope=api://${APP_REGISTRATION_ID}/mcp:tools"
```

#### Expected Response

```json
{
  "token_type": "Bearer",
  "expires_in": 3599,
  "access_token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIs..."
}
```

#### PowerShell Example

```powershell
# PowerShell alternative for Windows users
$tenantId = "your-tenant-id"
$clientId = "your-client-id"
$clientSecret = "your-client-secret"
$scope = "api://your-app-registration-id/mcp:tools"

$body = @{
    grant_type = "client_credentials"
    client_id = $clientId
    client_secret = $clientSecret
    scope = $scope
}

$response = Invoke-RestMethod -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" -Method Post -Body $body -ContentType "application/x-www-form-urlencoded"
$token = $response.access_token
Write-Host "Token: $token"
```

#### Token Validation

You can verify your token contains the required claims by decoding it at [jwt.ms](https://jwt.ms):

**Required claims:**
- `aud`: Should match `api://your-app-registration-id`
- `scp`: Should contain `mcp:tools`
- `iss`: Should be `https://login.microsoftonline.com/your-tenant-id/v2.0`
- `exp`: Token expiration time

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

The MCP Server requires proper configuration to authenticate with Microsoft Entra ID (formerly Azure AD). This configuration tells the server how to validate JWT tokens issued by your Entra ID tenant.

### appsettings.json Configuration

The server uses the following configuration structure in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "EntraId": {
    "Authority": "https://login.microsoftonline.com/{your-tenant-id}",
    "Audience": "api://{your-app-registration-id}"
  }
}
```

### Configuration Parameters

- **Authority**: The Entra ID endpoint that issues tokens for your tenant
  - Format: `https://login.microsoftonline.com/{tenant-id}`
  - Find your tenant ID: Azure Portal > Entra ID > Overview > Tenant ID
  
- **Audience**: The expected audience for tokens (must match your App Registration)
  - Format: `api://{app-registration-id}`
  - Find your app registration ID: Azure Portal > Entra ID > App registrations > Your app > Overview > Application (client) ID

### Environment-Specific Configuration

For different environments, you can override settings:

#### Development (appsettings.Development.json)
```json
{
  "EntraId": {
    "Authority": "https://login.microsoftonline.com/your-dev-tenant-id",
    "Audience": "api://your-dev-app-registration-id"
  }
}
```

#### Production (appsettings.Production.json)
```json
{
  "EntraId": {
    "Authority": "https://login.microsoftonline.com/your-prod-tenant-id",
    "Audience": "api://your-prod-app-registration-id"
  }
}
```

### Security Considerations

- **Never commit** client secrets or sensitive configuration to source control
- **Use Azure Key Vault** or environment variables for production secrets
- **Validate** that your Authority and Audience values are correct before deployment
- **Test** token validation in a development environment first

### Required Entra ID App Registration Setup

To properly configure authentication for the MCP Server, you need to create and configure an App Registration in Azure Portal. The MCP Server requires JWT tokens with specific scopes and claims for secure access to weather tools.

#### Step 1: Create an App Registration

1. **Sign in to Azure Portal** at [https://portal.azure.com](https://portal.azure.com)
2. **Navigate to** "Microsoft Entra ID" (formerly Azure Active Directory)
3. **Select** "App registrations" from the left menu
4. **Click** "New registration"
5. **Configure the registration**:
   - **Name**: `MCP Server API` (or your preferred name)
   - **Supported account types**: Choose based on your requirements:
     - "Accounts in this organizational directory only" (single tenant) - most common
     - "Accounts in any organizational directory" (multi-tenant) - if needed
   - **Redirect URI**: Leave blank for now (not needed for API access)
6. **Click** "Register"

#### Step 2: Configure App ID URI and Expose API

The MCP Server expects tokens with a specific audience format (`api://your-app-registration-id`).

1. **Go to** "Expose an API" in your app registration
2. **Click** "Set" next to "Application ID URI"
3. **Set the URI** to: `api://[your-application-id]`
   - Example: `api://12345678-1234-1234-1234-123456789012`
   - Use the Application (client) ID from your app registration's Overview page
4. **Save** the changes

#### Step 3: Add Custom Scope for MCP Tools

The MCP Server requires the `mcp:tools` scope for accessing weather tools.

1. **In "Expose an API"**, click "Add a scope"
2. **Configure the scope**:
   - **Scope name**: `mcp:tools`
   - **Who can consent**: Choose "Admins and users" (recommended)
   - **Admin consent display name**: `Access MCP Server Tools`
   - **Admin consent description**: `Allows the application to access MCP Server weather tools and functionality`
   - **User consent display name**: `Access weather tools`
   - **User consent description**: `Allows the application to access weather information through MCP Server`
   - **State**: Enabled
3. **Click** "Add scope"

#### Step 4: Configure API Permissions (for client applications)

If you have client applications that will request tokens:

1. **Go to** "API permissions"
2. **Click** "Add a permission"
3. **Select** "My APIs" tab
4. **Choose** your MCP Server API registration
5. **Select** "Delegated permissions" or "Application permissions" based on your scenario:
   - **Delegated permissions**: For user-context applications
   - **Application permissions**: For service-to-service scenarios (most common for MCP)
6. **Check** the `mcp:tools` scope
7. **Click** "Add permissions"
8. **Grant admin consent** if required (click "Grant admin consent for [your tenant]")

#### Step 5: Create Client Credentials (for service-to-service authentication)

For applications that will request tokens programmatically:

1. **Go to** "Certificates & secrets"
2. **Click** "New client secret"
3. **Configure the secret**:
   - **Description**: `MCP Server Client Secret`
   - **Expires**: Choose appropriate duration (6 months, 12 months, or 24 months)
4. **Click** "Add"
5. **Copy the secret value immediately** (it won't be shown again)
6. **Store securely** for use in token requests

#### Step 6: Configure Token Claims (Optional but Recommended)

To ensure proper scope claims in tokens:

1. **Go to** "Token configuration"
2. **Click** "Add optional claim"
3. **Select** "Access" token type
4. **Add** the following claims if not already present:
   - `aud` (Audience)
   - `scp` (Scope)
   - `roles` (if using application roles)
5. **Save** changes

#### Step 7: Update MCP Server Configuration

Update your `appsettings.json` with your specific tenant and app registration details:

```json
{
  "EntraId": {
    "Authority": "https://login.microsoftonline.com/YOUR-TENANT-ID",
    "Audience": "api://YOUR-APPLICATION-ID"
  }
}
```

**Replace**:
- `YOUR-TENANT-ID`: Your Azure AD tenant ID (found in Entra ID > Overview)
- `YOUR-APPLICATION-ID`: Your app registration's Application (client) ID

#### Verification Steps

1. **Test token request** using the example curl command (see "Getting a Token from Entra ID" section)
2. **Verify token contains**:
   - Correct `aud` (audience) claim: `api://your-application-id`
   - Correct `scp` (scope) claim: `mcp:tools`
   - Valid `iss` (issuer) claim: `https://login.microsoftonline.com/your-tenant-id/v2.0`
3. **Test API access** using the token with MCP Server endpoints

#### Common Configuration Issues

- **Invalid audience**: Ensure App ID URI matches the audience in appsettings.json
- **Missing scope**: Verify the `mcp:tools` scope is properly configured and granted
- **Token expiration**: Check token lifetime settings if tokens expire too quickly
- **Consent issues**: Ensure admin consent is granted for application permissions

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

## Troubleshooting

### Common Authentication Issues

#### 401 Unauthorized Errors

**Issue**: Getting "401 Unauthorized" when calling MCP Server endpoints

**Possible Causes & Solutions**:

1. **Missing or invalid token**
   - Ensure you're including the `Authorization: Bearer {token}` header
   - Verify token is not expired (check `exp` claim)
   - Regenerate token if expired

2. **Invalid audience**
   - Check that `appsettings.json` Audience matches your App ID URI
   - Verify App ID URI is set to `api://your-app-registration-id`
   - Ensure token request uses the correct scope format

3. **Missing scope**
   - Verify token contains `scp: mcp:tools` claim
   - Check that `mcp:tools` scope is properly configured in App Registration
   - Ensure client has permissions granted for the scope

#### 403 Forbidden Errors

**Issue**: Getting "403 Forbidden" when calling tool endpoints

**Possible Causes & Solutions**:

1. **Missing required scope**
   - Verify token contains `mcp:tools` in the `scp` claim
   - Check authorization policy configuration in the server

2. **Incorrect permission type**
   - Ensure you're using Application permissions for service-to-service scenarios
   - Grant admin consent if using Application permissions

#### Token Request Failures

**Issue**: Unable to get token from Entra ID

**Possible Causes & Solutions**:

1. **Invalid client credentials**
   - Verify Client ID is correct
   - Check that Client Secret hasn't expired
   - Generate new client secret if needed

2. **Incorrect scope format**
   - Use format: `api://your-app-registration-id/mcp:tools`
   - Ensure App ID URI is properly configured

3. **Tenant configuration**
   - Verify tenant ID is correct
   - Check that app registration exists in the correct tenant

### Debugging Tips

#### Validate Token Claims

Use [jwt.ms](https://jwt.ms) to decode and inspect your token claims:

Required claims for MCP Server:
```json
{
  "aud": "api://your-app-registration-id",
  "iss": "https://login.microsoftonline.com/your-tenant-id/v2.0",
  "scp": "mcp:tools",
  "exp": 1234567890
}
```

#### Enable Detailed Logging

Add to `appsettings.json` for more verbose authentication logs:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.Authentication": "Debug",
      "Microsoft.AspNetCore.Authorization": "Debug"
    }
  }
}
```

#### Test with curl

Basic connectivity test:

```bash
# Test without authentication (should work)
curl -X GET "http://localhost:5270/health"

# Test with authentication (should return 401 without valid token)
curl -X GET "http://localhost:5270/mcp/tools"

# Test with valid token (should return tools list)
curl -X GET "http://localhost:5270/mcp/tools" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### Getting Help

If you continue to experience issues:

1. **Check server logs** for detailed error messages
2. **Verify your App Registration** configuration matches the requirements
3. **Test token generation** independently using PowerShell or curl
4. **Validate token claims** using jwt.ms
5. **Review Entra ID audit logs** for authentication failures

## Security

- JWT tokens are validated against Entra ID for signature, issuer, audience, and expiration
- Tools require the `mcp:tools` scope from your Entra ID app registration
- HTTPS redirection is enabled for production security

### Production Security Checklist

For production deployment:

**Authentication & Authorization**:
- ✅ Configure your Entra ID tenant and app registration
- ✅ Set up proper API permissions and scopes
- ✅ Use production-grade secrets management (Azure Key Vault)
- ✅ Enable admin consent for application permissions
- ✅ Regularly rotate client secrets

**Network Security**:
- ✅ Enable HTTPS/TLS for all communications
- ✅ Configure appropriate CORS policies if needed
- ✅ Use network security groups to restrict access
- ✅ Consider using Azure Application Gateway or API Management

**Monitoring & Logging**:
- ✅ Enable application insights or similar monitoring
- ✅ Log authentication and authorization events
- ✅ Monitor for unusual access patterns
- ✅ Set up alerts for authentication failures
- ✅ Review Entra ID sign-in logs regularly

**Configuration Management**:
- ✅ Never commit secrets to source control
- ✅ Use environment-specific configurations
- ✅ Validate configuration values at startup
- ✅ Use managed identities where possible