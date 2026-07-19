namespace LayeredChat;

/// <summary>
/// Classifies a failed <see cref="ToolExecutionResult"/> so the orchestrator can decide whether to retry silently.
/// </summary>
public enum ToolFailureKind
{
    /// <summary>Not a failure, or unclassified (default).</summary>
    None,

    /// <summary>Temporary condition (timeout, rate limit, upstream 5xx); the orchestrator retries the same call once.</summary>
    Transient,

    /// <summary>The arguments or request were invalid; retrying identically will not help. Surfaced to the model.</summary>
    Invalid,

    /// <summary>Unrecoverable failure. Surfaced to the model without retry.</summary>
    Fatal
}
