using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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
            ValidateIssuerSigningKey = true,
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
        
        // Add event handlers for debugging JWT validation
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    Console.WriteLine($"JWT Authentication failed: {context.Exception}");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    Console.WriteLine("JWT Token validated successfully");
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
                if (builder.Environment.IsDevelopment())
                {
                    Console.WriteLine($"JWT Challenge: {context.Error} - {context.ErrorDescription}");
                }
                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                if (builder.Environment.IsDevelopment())
                {
                    Console.WriteLine("JWT Forbidden");
                }
                return Task.CompletedTask;
            }
        };
        
        // In development, accept any bearer token for testing
        if (builder.Environment.IsDevelopment())
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
    var authority = config["EntraId:Authority"];
    return Results.Ok(new
    {
        issuer = authority,
        authorization_endpoint = $"{authority}/oauth2/v2.0/authorize",
        token_endpoint = $"{authority}/oauth2/v2.0/token",
        userinfo_endpoint = $"{authority}/oidc/userinfo",
        jwks_uri = $"{authority}/discovery/v2.0/keys",
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
