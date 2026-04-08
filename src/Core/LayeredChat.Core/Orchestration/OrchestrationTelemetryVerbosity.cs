namespace LayeredChat;

/// <summary>
/// Controls how many <see cref="OrchestrationStreamEnvelope"/> items the orchestrator emits per turn.
/// </summary>
public enum OrchestrationTelemetryVerbosity
{
    /// <summary>
    /// Emit round lifecycle, usage, and message-commit envelopes (default).
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Emit only envelopes needed for tool UX, summaries, and completion (fewer items per turn).
    /// </summary>
    Minimal = 1
}
