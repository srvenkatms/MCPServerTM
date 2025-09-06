# Entra ID Configuration Example

To configure this MCP Server for production use with Microsoft Entra ID (formerly Azure AD), update the JWT authentication configuration in `Program.cs`:

## Replace the Development JWT Configuration

```csharp
// Remove the development configuration:
/*
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "your-256-bit-secret-key");
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "https://localhost:7000",
    ValidAudience = builder.Configuration["Jwt:Audience"] ?? "mcp-server-api",
    IssuerSigningKey = new SymmetricSecurityKey(key),
    ClockSkew = TimeSpan.Zero
};
*/

// Add Entra ID configuration:
options.Authority = builder.Configuration["EntraId:Authority"]; // https://login.microsoftonline.com/{tenant-id}
options.Audience = builder.Configuration["EntraId:Audience"];   // api://your-app-registration-id
options.TokenValidationParameters.ValidateIssuer = true;
options.TokenValidationParameters.ValidateAudience = true;
options.TokenValidationParameters.ValidateLifetime = true;
options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(5); // Allow 5 minutes clock skew
```

## Update appsettings.json

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
    "Audience": "api://your-app-registration-id",
    "SkipSignatureValidation": false
  }
}
```

## Skip JWT Signature Validation (For Network-Restricted Environments)

In production environments where the server cannot access Microsoft's public key endpoints (due to network restrictions, firewalls, or offline deployments), you can skip JWT signature validation while still validating other claims:

⚠️ **SECURITY WARNING**: Only use this feature in secure, network-isolated environments where you trust the token source.

```json
{
  "EntraId": {
    "Authority": "https://login.microsoftonline.com/{your-tenant-id}",
    "Audience": "api://your-app-registration-id",
    "SkipSignatureValidation": true
  }
}
```

When `SkipSignatureValidation` is set to `true`:
- JWT signature validation is bypassed
- All other validations still occur (issuer, audience, lifetime, role claims)
- The server can accept valid Entra ID tokens without internet access to Microsoft's JWKS endpoint
- Logs will show "Signature validation: SKIPPED" to indicate this mode is active

This feature is ideal for:
- Air-gapped environments
- Deployments behind restrictive firewalls
- Container environments with limited internet access
- Development/testing environments with network restrictions

## Required Entra ID App Registration Setup

1. **Create an App Registration** in the Azure Portal
2. **Set API Permissions**:
   - Add a custom scope: `mcp:tools`
3. **Configure Token Claims**:
   - Ensure the `scope` claim includes `mcp:tools`
4. **App ID URI**: Set to `api://your-app-registration-id`

## Client Token Request

Clients should request tokens with the `mcp:tools` scope:

```bash
# Example token request to Entra ID
curl -X POST "https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id={client-id}" \
  -d "client_secret={client-secret}" \
  -d "scope=api://your-app-registration-id/mcp:tools"
```

## OAuth Metadata Update

Update the OAuth metadata endpoint in `Program.cs` to reflect your Entra ID configuration:

```csharp
app.MapGet("/.well-known/oauth-authorization-server", () =>
{
    return Results.Ok(new
    {
        issuer = builder.Configuration["EntraId:Authority"],
        authorization_endpoint = $"{builder.Configuration["EntraId:Authority"]}/oauth2/v2.0/authorize",
        token_endpoint = $"{builder.Configuration["EntraId:Authority"]}/oauth2/v2.0/token",
        userinfo_endpoint = $"{builder.Configuration["EntraId:Authority"]}/oidc/userinfo",
        jwks_uri = $"{builder.Configuration["EntraId:Authority"]}/discovery/v2.0/keys",
        scopes_supported = new[] { "mcp:tools", "openid", "profile" },
        response_types_supported = new[] { "code", "token" },
        grant_types_supported = new[] { "authorization_code", "client_credentials" },
        token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" }
    });
});
```

## Security Considerations

- Remove the development token endpoint (`/dev/token`) in production
- Use HTTPS in production
- Validate that tokens contain the required `mcp:tools` scope
- Consider implementing rate limiting
- Log authentication events for security monitoring