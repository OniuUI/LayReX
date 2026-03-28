namespace LayeredChat;

/// <summary>
/// Tool executor that declines every call. Use for hosts with no tools or as a builder default when the manifest allow-list is empty.
/// </summary>
public sealed class NoOpToolExecutor : IToolExecutor
{
    private NoOpToolExecutor()
    {
    }

    /// <summary>
    /// Shared instance suitable for DI singleton registration.
    /// </summary>
    public static NoOpToolExecutor Instance { get; } = new();

    /// <inheritdoc />
    public Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolExecutionResult
        {
            Success = false,
            SummaryText = $"Tool '{toolName}' is not configured."
        });
    }
}
