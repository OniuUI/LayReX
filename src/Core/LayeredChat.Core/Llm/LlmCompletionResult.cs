namespace LayeredChat;

/// <summary>
/// Result of a single model completion step (text and/or tool calls).
/// </summary>
public sealed class LlmCompletionResult
{
    public string? TextContent { get; init; }

    public string? ReasoningContent { get; init; }

    public IReadOnlyList<ToolCallRequest> ToolCalls { get; init; } = [];

    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    /// <summary>Anthropic <c>cache_creation_input_tokens</c> when the provider reports them.</summary>
    public int CacheCreationInputTokens { get; init; }

    /// <summary>Anthropic <c>cache_read_input_tokens</c> when the provider reports them.</summary>
    public int CacheReadInputTokens { get; init; }
}
