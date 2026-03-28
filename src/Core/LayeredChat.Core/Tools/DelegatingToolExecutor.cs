namespace LayeredChat;

/// <summary>
/// Routes tool names to handlers first, then a fallback executor (for example domain tools plus data-plane tools).
/// </summary>
public sealed class DelegatingToolExecutor : IToolExecutor
{
    private readonly IReadOnlyDictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>
        _handlers;

    private readonly IToolExecutor _fallback;

    public DelegatingToolExecutor(
        IReadOnlyDictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>> handlers,
        IToolExecutor fallback)
    {
        _handlers = handlers;
        _fallback = fallback;
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default)
    {
        if (_handlers.TryGetValue(toolName, out var handler))
        {
            return handler(argumentsJson, session, cancellationToken);
        }

        return _fallback.ExecuteAsync(toolName, argumentsJson, session, cancellationToken);
    }
}
