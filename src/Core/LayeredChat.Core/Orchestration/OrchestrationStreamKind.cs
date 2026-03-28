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
