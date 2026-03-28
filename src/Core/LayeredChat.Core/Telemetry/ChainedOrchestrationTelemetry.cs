namespace LayeredChat;

/// <summary>
/// Fan-out telemetry to OpenTelemetry, bus publishers, and debug sinks without merging implementations.
/// </summary>
public sealed class ChainedOrchestrationTelemetry : IOrchestrationTelemetry
{
    private readonly IReadOnlyList<IOrchestrationTelemetry> _sinks;

    public ChainedOrchestrationTelemetry(IEnumerable<IOrchestrationTelemetry> sinks)
    {
        _sinks = sinks.ToList();
    }

    public async ValueTask EmitAsync(OrchestrationStreamEnvelope envelope, CancellationToken cancellationToken = default)
    {
        foreach (var sink in _sinks)
        {
            await sink.EmitAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }
}
