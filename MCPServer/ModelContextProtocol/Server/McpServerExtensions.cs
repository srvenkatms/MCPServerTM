using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModelContextProtocol.Server;

/// <summary>
/// MCP Server configuration and registration extensions
/// </summary>
public static class McpServerExtensions
{
    /// <summary>
    /// Adds MCP server services to the DI container
    /// </summary>
    public static IServiceCollection AddMcpServer(this IServiceCollection services)
    {
        services.AddSingleton<McpServerRegistry>();
        return services;
    }

    /// <summary>
    /// Maps MCP server endpoints
    /// </summary>
    public static IEndpointRouteBuilder MapMcpServer(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/mcp/tools", (McpServerRegistry registry) =>
        {
            var tools = registry.GetTools();
            return Results.Ok(tools);
        })
        .RequireAuthorization()
        .WithTags("MCP");

        endpoints.MapPost("/mcp/tools/{toolName}", async (string toolName, HttpContext context, McpServerRegistry registry) =>
        {
            // Debug: Log user claims in development
            if (context.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
            {
                var logger = context.RequestServices.GetService<ILogger<Program>>();
                if (logger != null)
                {
                    logger.LogInformation("User authentication status: {IsAuthenticated}", context.User?.Identity?.IsAuthenticated ?? false);
                    if (context.User?.Claims != null)
                    {
                        foreach (var claim in context.User.Claims)
                        {
                            logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                        }
                    }
                }
            }

            // Check for required role claim - try different claim types that Azure AD might use
            var hasRoleClaim = context.User.HasClaim("roles", "GetAlerts") ||
                              context.User.HasClaim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "GetAlerts") ||
                              context.User.HasClaim("role", "GetAlerts");

            if (!hasRoleClaim)
            {
                return Results.Forbid();
            }

            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var parameters = string.IsNullOrEmpty(body) 
                ? new Dictionary<string, object>() 
                : JsonSerializer.Deserialize<Dictionary<string, object>>(body) ?? new Dictionary<string, object>();

            var result = await registry.ExecuteTool(toolName, parameters);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithTags("MCP");

        return endpoints;
    }
}

/// <summary>
/// Registry for MCP tools
/// </summary>
public class McpServerRegistry
{
    private readonly Dictionary<string, ToolInfo> _tools = new();

    public McpServerRegistry()
    {
        RegisterTools();
    }

    private void RegisterTools()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);

        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null);

            foreach (var method in methods)
            {
                var description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var toolName = method.Name.ToLowerInvariant();
                
                _tools[toolName] = new ToolInfo
                {
                    Name = toolName,
                    Description = description,
                    Method = method
                };
            }
        }
    }

    public IEnumerable<object> GetTools()
    {
        return _tools.Values.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            parameters = GetParameterInfo(t.Method)
        });
    }

    public async Task<object> ExecuteTool(string toolName, Dictionary<string, object> parameters)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            throw new ArgumentException($"Tool '{toolName}' not found");
        }

        var methodParams = tool.Method.GetParameters();
        var args = new object[methodParams.Length];

        for (int i = 0; i < methodParams.Length; i++)
        {
            var param = methodParams[i];
            if (parameters.TryGetValue(param.Name!, out var value))
            {
                // Handle JSON element conversion
                if (value is JsonElement jsonElement)
                {
                    args[i] = ConvertJsonElement(jsonElement, param.ParameterType);
                }
                else
                {
                    args[i] = Convert.ChangeType(value, param.ParameterType);
                }
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue!;
            }
            else
            {
                throw new ArgumentException($"Required parameter '{param.Name}' not provided");
            }
        }

        var result = tool.Method.Invoke(null, args);
        
        if (result is Task task)
        {
            await task;
            var property = task.GetType().GetProperty("Result");
            return property?.GetValue(task) ?? new { success = true };
        }

        return result ?? new { success = true };
    }

    private static object ConvertJsonElement(JsonElement element, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return element.GetString() ?? "";
        }
        if (targetType == typeof(int))
        {
            return element.GetInt32();
        }
        if (targetType == typeof(int?))
        {
            return element.ValueKind == JsonValueKind.Null ? null : element.GetInt32();
        }
        if (targetType == typeof(bool))
        {
            return element.GetBoolean();
        }
        if (targetType == typeof(double))
        {
            return element.GetDouble();
        }
        if (targetType == typeof(decimal))
        {
            return element.GetDecimal();
        }
        
        // For nullable string types
        if (targetType == typeof(string) && element.ValueKind == JsonValueKind.Null)
        {
            return null!;
        }

        // Fallback to string representation
        return element.ToString();
    }

    private static object GetParameterInfo(MethodInfo method)
    {
        var parameters = method.GetParameters().Select(p => new
        {
            name = p.Name ?? "unknown",
            type = p.ParameterType.Name.ToLowerInvariant(),
            required = !p.HasDefaultValue,
            description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? ""
        });

        return new
        {
            type = "object",
            properties = parameters.ToDictionary(p => p.name, p => new
            {
                type = p.type,
                description = p.description
            }),
            required = parameters.Where(p => p.required).Select(p => p.name).ToArray()
        };
    }

    private class ToolInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public MethodInfo Method { get; set; } = null!;
    }
}
