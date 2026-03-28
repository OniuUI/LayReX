namespace LayeredChat;

/// <summary>
/// Receives the same <see cref="OrchestrationStreamEnvelope"/> stream as external gateways for logs, metrics, billing, and tracing.
/// See repository documentation <c>docs/TELEMETRY_AND_BILLING.md</c> for kind semantics, per-round vs per-turn tokens, and host integration patterns.
/// </summary>
public interface IOrchestrationTelemetry
{
    ValueTask EmitAsync(OrchestrationStreamEnvelope envelope, CancellationToken cancellationToken = default);
}
