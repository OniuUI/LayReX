namespace LayeredChat;

/// <summary>
/// Async stream unit for integrating services (HTTP SSE, message buses, external orchestrators). Fully async-safe; consumers map to their wire format.
/// </summary>
public sealed class OrchestrationStreamEnvelope
{
    public OrchestrationStreamKind Kind { get; init; }

    public long Sequence { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string RegistryKey { get; init; } = string.Empty;

    public string? OrchestrationId { get; init; }

    public string? SemanticVersion { get; init; }

    public string? TextDelta { get; init; }

    public ToolCallRequest? ToolCall { get; init; }

    public string? ToolName { get; init; }

    public ToolExecutionResult? ToolResult { get; init; }

    public ChatMessage? AppendedMessage { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}
