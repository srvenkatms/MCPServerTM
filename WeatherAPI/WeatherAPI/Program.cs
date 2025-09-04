using WeatherAPI.Models;
using WeatherAPI.Services;
using System.Reflection;

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

// Register HTTP clients
builder.Services.AddHttpClient<IMcpClientService, McpClientService>();

// Register services
builder.Services.AddScoped<IAgentFoundryService, AgentFoundryService>();
builder.Services.AddScoped<IMcpClientService, McpClientService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();

// Add health checks
builder.Services.AddHealthChecks();

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

// Add health check endpoint
app.MapHealthChecks("/health");

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
