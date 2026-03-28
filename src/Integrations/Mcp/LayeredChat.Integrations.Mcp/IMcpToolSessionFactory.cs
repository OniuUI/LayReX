namespace LayeredChat.Integrations.Mcp;

/// <summary>
/// Host-defined factory for creating MCP sessions (e.g. one stdio server per tenant or per request scope).
/// LayeredChat does not manage tenant isolation; the host defines what <c>scopeKey</c> means.
/// </summary>
public interface IMcpToolSessionFactory
{
    /// <summary>
    /// Creates or resolves an MCP session for the given scope (e.g. tenant id, user id, or connection id).
    /// </summary>
    /// <param name="scopeKey">Opaque scope chosen by the host.</param>
    Task<McpToolSession> CreateAsync(string scopeKey, CancellationToken cancellationToken = default);
}
