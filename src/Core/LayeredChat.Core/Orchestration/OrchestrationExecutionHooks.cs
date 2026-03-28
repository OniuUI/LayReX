namespace LayeredChat;

/// <summary>
/// Optional cross-cutting behavior for a turn: telemetry, external HTTP delegation, and outbound correlation.
/// </summary>
public sealed class OrchestrationExecutionHooks
{
    public IOrchestrationTelemetry? Telemetry { get; init; }

    public IHttpOrchestrationForwarder? HttpForwarder { get; init; }
}
