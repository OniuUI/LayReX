namespace LayeredChat;

/// <summary>
/// Result of executing a tool for the model (typically serialized to tool-role message content).
/// </summary>
public sealed class ToolExecutionResult
{
    public bool Success { get; init; } = true;

    public string SummaryText { get; init; } = string.Empty;

    /// <summary>
    /// Optional JSON or structured payload for hosts that stream artifacts alongside text.
    /// </summary>
    public string? StructuredPayloadJson { get; init; }
}
