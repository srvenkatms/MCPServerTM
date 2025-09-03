using System;

namespace ModelContextProtocol;

/// <summary>
/// Marks a method as an MCP server tool
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class McpServerToolAttribute : Attribute
{
}