namespace LayeredChat;

/// <summary>
/// Receives the same <see cref="OrchestrationStreamEnvelope"/> stream as external gateways for logs, metrics, and tracing.
/// </summary>
public interface IOrchestrationTelemetry
{
    ValueTask EmitAsync(OrchestrationStreamEnvelope envelope, CancellationToken cancellationToken = default);
}
