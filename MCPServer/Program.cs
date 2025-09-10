using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;
using MCPServer.Services;
using MCPServer.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    // Check for Azure standard environment variables first (without underscores), 
    // then legacy names (with underscores), then fallback to configuration
    var connectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTIONSTRING") 
                        ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING") 
                        ?? builder.Configuration["ApplicationInsights:ConnectionString"];
    var instrumentationKey = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_INSTRUMENTATIONKEY")
                           ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_INSTRUMENTATION_KEY")
                           ?? builder.Configuration["ApplicationInsights:InstrumentationKey"];
    
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.ConnectionString = connectionString;
    }
#pragma warning disable CS0618 // Type or member is obsolete
    else if (!string.IsNullOrEmpty(instrumentationKey))
    {
        options.InstrumentationKey = instrumentationKey;
    }
#pragma warning restore CS0618 // Type or member is obsolete
    
    // Configure telemetry modules based on settings
    options.EnableRequestTrackingTelemetryModule = builder.Configuration.GetValue<bool>("ApplicationInsights:EnableRequestTrackingTelemetryModule", true);
    options.EnableDependencyTrackingTelemetryModule = builder.Configuration.GetValue<bool>("ApplicationInsights:EnableDependencyTrackingTelemetryModule", true);
    options.EnablePerformanceCounterCollectionModule = builder.Configuration.GetValue<bool>("ApplicationInsights:EnablePerformanceCounterCollectionModule", true);
    options.EnableEventCounterCollectionModule = builder.Configuration.GetValue<bool>("ApplicationInsights:EnableEventCounterCollectionModule", true);
    options.EnableHeartbeat = builder.Configuration.GetValue<bool>("ApplicationInsights:EnableHeartbeat", true);
    // Note: EnableAzureInstanceMetadata is not available in ApplicationInsightsServiceOptions
    // Azure instance metadata collection is automatically enabled when running in Azure
});

// Add custom MCP telemetry service with fallback for missing TelemetryClient
builder.Services.AddSingleton<McpTelemetryService>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var logger = provider.GetRequiredService<ILogger<McpTelemetryService>>();
    var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
    
    // Try to get TelemetryClient, but don't fail if it's not available
    var telemetryClient = provider.GetService<Microsoft.ApplicationInsights.TelemetryClient>();
    
    return new McpTelemetryService(configuration, logger, httpContextAccessor, telemetryClient);
});

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "MCP Server API", 
        Version = "v1",
        Description = "Model Context Protocol Server with OAuth 2.0 protection"
    });
    
    // Add JWT Bearer authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure JWT Bearer Authentication for Entra ID
var skipSignatureValidation = builder.Configuration.GetValue<bool>("EntraId:SkipSignatureValidation");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Configure for Entra ID (Azure AD)
        options.Authority = builder.Configuration["EntraId:Authority"];
        // options.Audience = builder.Configuration["EntraId:Audience"]; // Commented out audience check
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["EntraId:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = !skipSignatureValidation, // Skip signature validation if configured
            RequireSignedTokens = !skipSignatureValidation, // Don't require signed tokens if skipping validation
            ClockSkew = TimeSpan.FromMinutes(5), // Allow 5 minutes clock skew for Azure AD
            
            // Azure AD tokens use https://sts.windows.net/{tenant-id}/ as issuer
            // while Authority is configured as https://login.microsoftonline.com/{tenant-id}/v2.0/
            // We need to explicitly accept the sts.windows.net issuer format
            ValidIssuers = new[]
            {
                // Extract tenant ID from authority and create sts.windows.net issuer
                builder.Configuration["EntraId:Authority"]?
                    .Replace("https://login.microsoftonline.com/", "https://sts.windows.net/")
                    .Replace("/v2.0/", "/")
                    .TrimEnd('/') + "/",
                    
                // Also accept the authority as-is in case the format changes
                builder.Configuration["EntraId:Authority"]?.TrimEnd('/')
            }.Where(i => !string.IsNullOrEmpty(i)).ToArray(),
            
            // Map role claims properly for Azure AD
            RoleClaimType = "roles"
        };
        
        // If signature validation is skipped, we need to provide a key resolver that doesn't validate
        if (skipSignatureValidation)
        {
            options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
            {
                // Return a dummy key that will pass validation
                return new List<SecurityKey> { new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("dummy-key-for-skipped-validation-32-chars-long-enough")) };
            };
            
            // Disable metadata retrieval since we're not validating signatures
            options.RequireHttpsMetadata = false;
            options.MetadataAddress = null;
            // Additional settings to bypass signature validation
            options.TokenValidationParameters.SignatureValidator = (token, parameters) =>
            {
                // Return the token as-is without validating signature
                return new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
            };
        }
        
        // Add event handlers for debugging JWT validation and telemetry tracking
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Track authentication failure
                var telemetryService = context.HttpContext.RequestServices.GetService<McpTelemetryService>();
                var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString();
                telemetryService?.TrackAuthenticationEvent("AuthenticationFailed", context.Principal, false, context.Exception?.Message, correlationId);
                
                if (builder.Environment.IsDevelopment() || skipSignatureValidation)
                {
                    Console.WriteLine($"JWT Authentication failed: {context.Exception}");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Track successful authentication
                var telemetryService = context.HttpContext.RequestServices.GetService<McpTelemetryService>();
                var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString();
                telemetryService?.TrackAuthenticationEvent("TokenValidated", context.Principal, true, null, correlationId);
                
                if (builder.Environment.IsDevelopment() || skipSignatureValidation)
                {
                    Console.WriteLine($"JWT Token validated successfully (Signature validation: {(!skipSignatureValidation ? "enabled" : "SKIPPED")})");
                    Console.WriteLine($"Claims count: {context.Principal?.Claims?.Count() ?? 0}");
                    foreach (var claim in context.Principal?.Claims ?? Enumerable.Empty<System.Security.Claims.Claim>())
                    {
                        Console.WriteLine($"  {claim.Type}: {claim.Value}");
                    }
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                // Track authentication challenge
                var telemetryService = context.HttpContext.RequestServices.GetService<McpTelemetryService>();
                var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString();
                telemetryService?.TrackAuthenticationEvent("Challenge", null, false, $"{context.Error} - {context.ErrorDescription}", correlationId);
                
                if (builder.Environment.IsDevelopment() || skipSignatureValidation)
                {
                    Console.WriteLine($"JWT Challenge: {context.Error} - {context.ErrorDescription}");
                }
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                // Track forbidden access
                var telemetryService = context.HttpContext.RequestServices.GetService<McpTelemetryService>();
                var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString();
                telemetryService?.TrackAuthenticationEvent("Forbidden", context.HttpContext.User, false, null, correlationId);
                
                if (builder.Environment.IsDevelopment() || skipSignatureValidation)
                {
                    Console.WriteLine("JWT Forbidden");
                }
                return Task.CompletedTask;
            }
        };
        
        // In development, accept any bearer token for testing
        if (builder.Environment.IsDevelopment() && !skipSignatureValidation)
        {
            // Keep existing development override but add logging
            var originalOnTokenValidated = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                await originalOnTokenValidated(context);
                // For development, create a basic identity with the required claim
                var claims = new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim("scope", "mcp:tools")
                };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, "Bearer");
                context.Principal = new System.Security.Claims.ClaimsPrincipal(identity);
                Console.WriteLine("Development mode: Overriding token validation");
            };
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GetAlertsRole", policy =>
        policy.RequireClaim("roles", "GetAlerts"));
});

// Add MCP Server services
builder.Services.AddMcpServer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add telemetry middleware before authentication
app.UseMiddleware<TelemetryMiddleware>();

// Add authentication and authorization middleware
app.UseAuthentication();

// Development middleware to bypass authentication for development tokens
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (token == "dev-token-123")
            {
                // Create a development identity with the required role claim
                var claims = new List<System.Security.Claims.Claim>
                {
                    new System.Security.Claims.Claim("roles", "GetAlerts")
                };
                var identity = new System.Security.Claims.ClaimsIdentity(claims, "DevToken");
                context.User = new System.Security.Claims.ClaimsPrincipal(identity);
            }
        }
        await next();
    });
}

app.UseAuthorization();

// OAuth resource metadata endpoint for client discovery
app.MapGet("/.well-known/oauth-authorization-server", (IConfiguration config) =>
{
    var authority = config["EntraId:Authority"]?.TrimEnd('/');
    var baseAuthority = authority?.Replace("/v2.0", ""); // Remove v2.0 for endpoint construction
    return Results.Ok(new
    {
        issuer = authority,
        authorization_endpoint = $"{baseAuthority}/oauth2/v2.0/authorize",
        token_endpoint = $"{baseAuthority}/oauth2/v2.0/token",
        userinfo_endpoint = $"{baseAuthority}/oidc/userinfo",
        jwks_uri = $"{baseAuthority}/discovery/v2.0/keys",
        roles_supported = new[] { "GetAlerts" },
        response_types_supported = new[] { "code", "token" },
        grant_types_supported = new[] { "authorization_code", "client_credentials" },
        token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" }
    });
}).WithTags("OAuth");

// MCP Server info endpoint
app.MapGet("/mcp/info", () =>
{
    return Results.Ok(new
    {
        name = "MCP Weather Server",
        version = "1.0.0",
        description = "Model Context Protocol server providing weather tools with OAuth 2.0 protection",
        protocol_version = "1.0",
        supported_protocols = new[] { "http", "https" },
        authentication = new
        {
            type = "oauth2",
            required_roles = new[] { "GetAlerts" }
        },
        capabilities = new
        {
            tools = true,
            resources = false,
            prompts = false
        }
    });
}).WithTags("MCP");

// Map MCP Server endpoints
app.MapMcpServer();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithTags("Health");

// Note: Development token endpoint removed - use Entra ID for token generation

// Development token endpoint for testing (only in Development environment)
if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/token", () => 
    {
        return Results.Ok(new 
        { 
            access_token = "dev-token-123",
            token_type = "Bearer",
            expires_in = 3600,
            roles = new[] { "GetAlerts" }
        });
    }).WithTags("Development");
}

app.Run();
