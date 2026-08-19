namespace LayeredChat;

/// <summary>
/// A single message in the conversation, including optional tool-call metadata for assistant and tool roles.
/// </summary>
public sealed class ChatMessage
{
    public ChatRole Role { get; init; }

    public string Content { get; init; } = string.Empty;

    public string? ReasoningContent { get; init; }

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    public IReadOnlyList<ToolCallRequest>? ToolCalls { get; init; }

    /// <summary>
    /// When <see cref="Role"/> is <see cref="ChatRole.Tool"/>, connectors map this to the provider tool-error flag
    /// (Anthropic <c>is_error</c>).
    /// </summary>
    public bool IsError { get; init; }
}
