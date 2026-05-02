using System.Collections.Generic;

namespace LayeredChat.StageChecks;

/// <summary>
/// Inputs for a stage checkpoint. All strings are bounded by the host before invocation.
/// </summary>
public sealed class StageCheckpointContext
{
    public required string StageKey { get; init; }

    public string OrchestrationId { get; init; } = "";

    public string RegistryKey { get; init; } = "";

    public string CorrelationId { get; init; } = "";

    public int ToolRoundIndex { get; init; }

    public IReadOnlyDictionary<string, string> Metrics { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
