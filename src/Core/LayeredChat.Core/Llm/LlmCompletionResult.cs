namespace LayeredChat;

/// <summary>
/// Result of a single model completion step (text and/or tool calls).
/// </summary>
public sealed class LlmCompletionResult
{
    public string? TextContent { get; init; }

    public IReadOnlyList<ToolCallRequest> ToolCalls { get; init; } = [];

    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }
}
