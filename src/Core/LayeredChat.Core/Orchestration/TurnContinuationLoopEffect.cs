namespace LayeredChat;

/// <summary>
/// How the orchestrator should proceed after an assistant message with no tool calls and optional turn continuation evaluation.
/// </summary>
public enum TurnContinuationLoopEffect
{
    /// <summary>End the model/tool loop after this assistant message.</summary>
    CompleteTurn = 0,

    /// <summary>
    /// Append <see cref="TurnContinuationEvaluationResult.MessagesToInjectBeforeNextModelRound"/> and run another model round (consumes one iteration toward max rounds).
    /// </summary>
    ContinueWithInjectedMessages = 1,

    /// <summary>
    /// End the turn without another model round; evaluation metadata is still emitted on <see cref="OrchestrationStreamKind.CompletionEvaluationFinished"/> for host observability.
    /// </summary>
    AdvisoryOnly = 2
}
