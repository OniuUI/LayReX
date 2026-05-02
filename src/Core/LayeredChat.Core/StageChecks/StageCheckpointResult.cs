namespace LayeredChat.StageChecks;

/// <summary>
/// Result of a stage checkpoint. Retry is a hint for host-driven loops; the core library does not enforce retries without host integration.
/// </summary>
public sealed class StageCheckpointResult
{
    public bool Pass { get; init; }

    public bool SuggestRetry { get; init; }

    public string? Rationale { get; init; }
}
