using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ModelContextProtocol.Server;
using System.Security.Claims;

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

// Configure JWT Bearer Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // For development/testing - use a simple symmetric key
        // In production, you would configure this for Entra ID
        var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "your-256-bit-secret-key-here-that-is-long-enough-for-hs256");
        
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

        // For production Entra ID integration, you would use:
        // options.Authority = "https://login.microsoftonline.com/{tenant-id}";
        // options.Audience = "api://your-app-id";
        // options.TokenValidationParameters.ValidateIssuer = true;
        // options.TokenValidationParameters.ValidateAudience = true;
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
    return Results.Ok(new
    {
        issuer = "https://localhost:7000",
        authorization_endpoint = "https://localhost:7000/oauth/authorize",
        token_endpoint = "https://localhost:7000/oauth/token",
        userinfo_endpoint = "https://localhost:7000/oauth/userinfo",
        jwks_uri = "https://localhost:7000/.well-known/jwks.json",
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

// Development token endpoint for testing
if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/token", (TokenRequest request) =>
    {
        var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "your-256-bit-secret-key-here-that-is-long-enough-for-hs256");
        var issuer = builder.Configuration["Jwt:Issuer"] ?? "https://localhost:7000";
        var audience = builder.Configuration["Jwt:Audience"] ?? "mcp-server-api";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserId ?? "test-user"),
            new(ClaimTypes.Name, request.Username ?? "Test User"),
            new("scope", "mcp:tools")
        };

        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Results.Ok(new
        {
            access_token = tokenString,
            token_type = "Bearer",
            expires_in = 3600,
            scope = "mcp:tools"
        });
    }).WithTags("Development");
}

app.Run();

public record TokenRequest(string? UserId = null, string? Username = null);
