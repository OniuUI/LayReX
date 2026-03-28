namespace LayeredChat;

/// <summary>
/// Optional cross-cutting behavior for a turn: telemetry, external HTTP delegation, and outbound correlation.
/// </summary>
public sealed class OrchestrationExecutionHooks
{
    public IOrchestrationTelemetry? Telemetry { get; init; }

    public IHttpOrchestrationForwarder? HttpForwarder { get; init; }

    /// <summary>
    /// Optional per-round narrowing or expansion of tools (subset of manifest allow-list).
    /// </summary>
    public IToolRoundCatalogProvider? ToolRoundCatalogProvider { get; init; }
}
