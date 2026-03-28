namespace LayeredChat;

/// <summary>
/// No-op telemetry sink for hosts that do not record orchestration events.
/// </summary>
public sealed class NullOrchestrationTelemetry : IOrchestrationTelemetry
{
    public static readonly NullOrchestrationTelemetry Instance = new();

    public ValueTask EmitAsync(OrchestrationStreamEnvelope envelope, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
