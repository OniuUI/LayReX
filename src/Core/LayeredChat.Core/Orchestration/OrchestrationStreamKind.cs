namespace LayeredChat;

/// <summary>
/// High-level events surfaced to gateways, SSE bridges, and observability sinks for a single orchestrated turn.
/// </summary>
public enum OrchestrationStreamKind
{
    TurnStarted,
    ContextSlicesReady,
    ForwardingToExternal,
    ExternalForwardCompleted,
    ModelRoundStarted,
    /// <summary>
    /// Emitted after a model round completes; <see cref="OrchestrationStreamEnvelope.Attributes"/> include round index and cumulative token totals when available.
    /// </summary>
    ModelRoundCompleted,
    AssistantTextDelta,
    ToolCallFinished,
    ToolExecutionStarted,
    ToolExecutionFinished,
    AssistantMessageCommitted,
    UsageUpdate,
    TurnCompleted,
    TurnResultSummary,
    Error
}
