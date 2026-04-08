namespace LayeredChat;

/// <summary>
/// Result of <see cref="ITurnContinuationEvaluator.EvaluateAfterAssistantRoundAsync"/>; drives whether the orchestrator stops or continues after a no-tool assistant reply.
/// </summary>
public sealed class TurnContinuationEvaluationResult
{
    /// <summary>
    /// When <see cref="TurnContinuationLoopEffect.ContinueWithInjectedMessages"/>, non-empty messages appended before the next model call.
    /// Ignored for <see cref="TurnContinuationLoopEffect.CompleteTurn"/> and <see cref="TurnContinuationLoopEffect.AdvisoryOnly"/>.
    /// </summary>
    public IReadOnlyList<ChatMessage>? MessagesToInjectBeforeNextModelRound { get; init; }

    /// <summary>
    /// How the orchestrator should proceed. Defaults to <see cref="TurnContinuationLoopEffect.CompleteTurn"/>.
    /// </summary>
    public TurnContinuationLoopEffect LoopEffect { get; init; } = TurnContinuationLoopEffect.CompleteTurn;

    /// <summary>
    /// Optional small key-value pairs copied into telemetry (truncated by the orchestrator). Use for verdict codes, rubric ids, short reasons.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
