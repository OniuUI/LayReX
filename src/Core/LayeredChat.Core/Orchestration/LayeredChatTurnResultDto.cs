namespace LayeredChat;

/// <summary>
/// JSON projection of <see cref="LayeredChatTurnResult"/> for HTTP pods and forwarders.
/// </summary>
public sealed class LayeredChatTurnResultDto
{
    public string RegistryKey { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string? OrchestrationId { get; init; }

    public string? SemanticVersion { get; init; }

    public string? AssistantText { get; init; }

    public IReadOnlyList<ChatMessageDto> AppendedMessages { get; init; } = [];

    public int TotalInputTokens { get; init; }

    public int TotalOutputTokens { get; init; }
}
