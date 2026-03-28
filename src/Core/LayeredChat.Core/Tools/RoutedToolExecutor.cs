namespace LayeredChat;

/// <summary>
/// Dispatches tool execution to the first matching route. Use with MCP-prefixed tools and a host <see cref="IToolExecutor"/> fallback.
/// </summary>
public sealed class RoutedToolExecutor : IToolExecutor
{
    private readonly IReadOnlyList<(Func<string, bool> Route, IToolExecutor Executor)> _routes;

    public RoutedToolExecutor(IReadOnlyList<(Func<string, bool> Route, IToolExecutor Executor)> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        _routes = routes;
        if (_routes.Count == 0)
        {
            throw new ArgumentException("At least one route is required.", nameof(routes));
        }
    }

    /// <summary>
    /// Routes tools whose names start with <paramref name="prefix"/> to <paramref name="prefixedExecutor"/>; all others to <paramref name="defaultExecutor"/>.
    /// </summary>
    public static RoutedToolExecutor FromPrefix(
        string prefix,
        StringComparison comparison,
        IToolExecutor prefixedExecutor,
        IToolExecutor defaultExecutor)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(prefixedExecutor);
        ArgumentNullException.ThrowIfNull(defaultExecutor);
        return new RoutedToolExecutor(
        [
            (name => name.StartsWith(prefix, comparison), prefixedExecutor),
            (_ => true, defaultExecutor)
        ]);
    }

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default)
    {
        foreach (var (route, executor) in _routes)
        {
            if (route(toolName))
            {
                return await executor
                    .ExecuteAsync(toolName, argumentsJson, session, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return new ToolExecutionResult
        {
            Success = false,
            SummaryText = $"No executor route matched tool '{toolName}'."
        };
    }
}
