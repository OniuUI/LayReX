namespace LayeredChat;

/// <summary>
/// Read-only context for turn continuation evaluation after an assistant completion with no tool calls.
/// </summary>
public sealed class TurnContinuationEvaluationContext
{
    public required OrchestrationProfileManifest Manifest { get; init; }

    public required LayeredChatTurnRequest Request { get; init; }

    public required OrchestrationSessionContext Session { get; init; }

    /// <summary>0-based model round index (same as orchestrator loop variable).</summary>
    public required int RoundIndex { get; init; }

    /// <summary>Working messages including system, history, user turn, tool messages, and the assistant message just committed for this round.</summary>
    public required IReadOnlyList<ChatMessage> WorkingTranscript { get; init; }

    public required LlmCompletionResult LastCompletion { get; init; }

    public required int CumulativeInputTokens { get; init; }

    public required int CumulativeOutputTokens { get; init; }

    public required string CorrelationId { get; init; }
}
