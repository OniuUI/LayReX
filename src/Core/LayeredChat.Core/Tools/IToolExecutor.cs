namespace LayeredChat;

/// <summary>
/// Host-implemented tool execution surface. The orchestration layer resolves tool names from the manifest to this executor.
/// </summary>
public interface IToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default);
}
