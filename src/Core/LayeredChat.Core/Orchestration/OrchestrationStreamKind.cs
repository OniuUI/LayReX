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
    /// <summary>
    /// Turn continuation evaluation started; see TELEMETRY_AND_BILLING.md for attributes.
    /// </summary>
    CompletionEvaluationStarted,
    /// <summary>
    /// Turn continuation evaluation finished; attributes include loop effect and optional metadata.
    /// </summary>
    CompletionEvaluationFinished,
    Error
}
