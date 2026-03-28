namespace LayeredChat;

/// <summary>
/// A single message in the conversation, including optional tool-call metadata for assistant and tool roles.
/// </summary>
public sealed class ChatMessage
{
    public ChatRole Role { get; init; }

    public string Content { get; init; } = string.Empty;

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    public IReadOnlyList<ToolCallRequest>? ToolCalls { get; init; }
}
