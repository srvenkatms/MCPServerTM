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
        options.Audience = builder.Configuration["EntraId:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew for Azure AD
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpTools", policy =>
        policy.RequireClaim("scope", "mcp:tools"));
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
app.UseAuthorization();

// OAuth resource metadata endpoint for client discovery
app.MapGet("/.well-known/oauth-authorization-server", () =>
{
    var authority = builder.Configuration["EntraId:Authority"];
    return Results.Ok(new
    {
        issuer = authority,
        authorization_endpoint = $"{authority}/oauth2/v2.0/authorize",
        token_endpoint = $"{authority}/oauth2/v2.0/token",
        userinfo_endpoint = $"{authority}/oidc/userinfo",
        jwks_uri = $"{authority}/discovery/v2.0/keys",
        scopes_supported = new[] { "mcp:tools", "openid", "profile" },
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
            required_scopes = new[] { "mcp:tools" }
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

app.Run();
