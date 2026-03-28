namespace LayeredChat;

/// <summary>
/// Outcome of a single orchestrated turn: assistant-visible text (if any) and messages to append after the user message.
/// </summary>
public sealed class LayeredChatTurnResult
{
    public string RegistryKey { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string? OrchestrationId { get; init; }

    public string? SemanticVersion { get; init; }

    public string? AssistantText { get; init; }

    public IReadOnlyList<ChatMessage> AppendedMessages { get; init; } = [];

    public int TotalInputTokens { get; init; }

    public int TotalOutputTokens { get; init; }
}
