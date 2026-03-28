using LayeredChat;

namespace LayeredChat.Integrations.Mcp;

/// <summary>
/// Helpers to combine host tools with an <see cref="McpToolSession"/>.
/// </summary>
public static class McpOrchestrationWiring
{
    /// <summary>
    /// Host catalog first so host tool names can shadow MCP names; MCP fills gaps for the same allowed list.
    /// </summary>
    public static IToolCatalog CombineHostCatalogThenMcp(IToolCatalog hostCatalog, IToolCatalog mcpCatalog)
    {
        ArgumentNullException.ThrowIfNull(hostCatalog);
        ArgumentNullException.ThrowIfNull(mcpCatalog);
        return new CompositeToolCatalog([hostCatalog, mcpCatalog]);
    }

    /// <summary>
    /// Routes tools whose names start with <paramref name="mcpToolNamePrefix"/> to the MCP session executor; others to the host executor.
    /// </summary>
    public static RoutedToolExecutor RoutePrefixedMcpThenHost(
        string mcpToolNamePrefix,
        IToolExecutor mcpExecutor,
        IToolExecutor hostExecutor)
    {
        return RoutedToolExecutor.FromPrefix(
            mcpToolNamePrefix,
            StringComparison.Ordinal,
            mcpExecutor,
            hostExecutor);
    }
}
