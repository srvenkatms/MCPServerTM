using System;

namespace ModelContextProtocol;

/// <summary>
/// Marks a class as containing MCP server tools
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class McpServerToolTypeAttribute : Attribute
{
}