namespace LayeredChat;

/// <summary>
/// A dispatchable specialist: a child orchestration with its own manifest, system prompt, model and tool surface.
/// The parent model invokes it through the <c>dispatch_agent</c> tool; the child runs with a fresh transcript.
/// </summary>
public sealed class SubAgentDefinition
{
    public required string Name { get; init; }

    /// <summary>One-line description rendered into the dispatch tool schema; the parent model routes on it.</summary>
    public required string Description { get; init; }

    /// <summary>Registry key of the child orchestration manifest ({id}@{semver}).</summary>
    public required string OrchestrationRegistryKey { get; init; }

    /// <summary>The agent's system prompt (hosts typically compose this from bundles).</summary>
    public string SystemInstructionText { get; init; } = string.Empty;

    /// <summary>Optional model override resolved by the host (e.g. tier mapping to the tenant's provider).</summary>
    public string? ModelNameOverride { get; init; }

    /// <summary>Optional per-agent connector option overrides (temperature, max output tokens, adapter profile).</summary>
    public LlmRequestOptions? ConnectorOptions { get; init; }

    /// <summary>When set, the child's final answer is schema-enforced and returned as the structured payload.</summary>
    public ResponseSchemaSpec? ResultSchema { get; init; }
}
