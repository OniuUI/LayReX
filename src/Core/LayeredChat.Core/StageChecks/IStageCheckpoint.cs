namespace LayeredChat.StageChecks;

/// <summary>
/// Host-supplied checkpoint for a named orchestration stage (tool round, synthesis, etc.). Implementations stay domain-agnostic; hosts map stages to business rules.
/// </summary>
public interface IStageCheckpoint
{
    /// <summary>
    /// Evaluates whether the current stage output satisfies policy. <see cref="StageCheckpointResult.SuggestRetry"/> is advisory until the host loop honors it.
    /// </summary>
    ValueTask<StageCheckpointResult> EvaluateAsync(StageCheckpointContext context, CancellationToken cancellationToken = default);
}
