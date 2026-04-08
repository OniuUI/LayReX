namespace LayeredChat;

/// <summary>
/// Host-provided hook to decide whether a no-tool assistant reply ends the turn, should trigger another model round (with injected messages), or ends with advisory metadata only.
/// </summary>
public interface ITurnContinuationEvaluator
{
    /// <summary>Short id for telemetry attributes (e.g. type name or config key).</summary>
    string EvaluatorId { get; }

    ValueTask<TurnContinuationEvaluationResult> EvaluateAfterAssistantRoundAsync(
        TurnContinuationEvaluationContext context,
        CancellationToken cancellationToken = default);
}
