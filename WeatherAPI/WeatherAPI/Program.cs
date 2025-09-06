using WeatherAPI.Models;
using WeatherAPI.Services;
using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Weather API",
        Version = "v1",
        Description = "Weather API that uses Agent Foundry SDK and MCP Server to provide weather information for cities"
    });
    
    // Add XML comments for better API documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Configure strongly-typed configuration
builder.Services.Configure<AgentFoundryConfig>(
    builder.Configuration.GetSection("AgentFoundry"));
builder.Services.Configure<McpServerConfig>(
    builder.Configuration.GetSection("McpServer"));
builder.Services.Configure<WeatherPromptsConfig>(
    builder.Configuration.GetSection("WeatherPrompts"));
builder.Services.Configure<RetryConfig>(
    builder.Configuration.GetSection("Retry"));

// Register configuration instances for dependency injection
builder.Services.AddSingleton(provider => 
    provider.GetRequiredService<IConfiguration>()
        .GetSection("AgentFoundry")
        .Get<AgentFoundryConfig>() ?? new AgentFoundryConfig());

builder.Services.AddSingleton(provider => 
    provider.GetRequiredService<IConfiguration>()
        .GetSection("McpServer")
        .Get<McpServerConfig>() ?? new McpServerConfig());

builder.Services.AddSingleton(provider => 
    provider.GetRequiredService<IConfiguration>()
        .GetSection("WeatherPrompts")
        .Get<WeatherPromptsConfig>() ?? new WeatherPromptsConfig());

builder.Services.AddSingleton(provider => 
    provider.GetRequiredService<IConfiguration>()
        .GetSection("Retry")
        .Get<RetryConfig>() ?? new RetryConfig());

// Register HTTP clients
builder.Services.AddHttpClient<McpClientService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30); // 30 second timeout
});
builder.Services.AddHttpClient<McpServerHealthCheck>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10); // 10 second timeout for health checks
});

// Register MCP client service with resilient wrapper
builder.Services.AddScoped<McpClientService>();
builder.Services.AddScoped<IMcpClientService>(provider =>
{
    var innerService = provider.GetRequiredService<McpClientService>();
    var retryConfig = provider.GetRequiredService<RetryConfig>();
    var logger = provider.GetRequiredService<ILogger<ResilientMcpClientService>>();
    return new ResilientMcpClientService(innerService, retryConfig, logger);
});

// Register services
builder.Services.AddScoped<IAgentFoundryService, AgentFoundryService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<ConfigurationValidator>("configuration")
    .AddCheck<McpServerHealthCheck>("mcp-server");

// Add CORS support for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Weather API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
    app.UseCors("AllowAll");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint with details
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            service = "Weather API",
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds,
                exception = entry.Value.Exception?.Message
            })
        };
        
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
});

// Add basic info endpoint
app.MapGet("/", () => new
{
    service = "Weather API",
    version = "1.0.0",
    description = "Weather API using Agent Foundry SDK and MCP Server",
    timestamp = DateTime.UtcNow,
    endpoints = new
    {
        weather = "/api/weather/{city}",
        current = "/api/weather/{city}/current",
        forecast = "/api/weather/{city}/forecast",
        alerts = "/api/weather/{city}/alerts",
        health = "/health",
        swagger = "/swagger"
    }
}).WithTags("Info");

app.Run();
