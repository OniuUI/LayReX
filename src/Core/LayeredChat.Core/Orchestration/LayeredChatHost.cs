using System.Runtime.CompilerServices;

namespace LayeredChat;

/// <summary>
/// Application-facing entry type: wraps <see cref="LayeredChatOrchestrator"/> for typical host usage (single place to call into LayeredChat).
/// </summary>
public sealed class LayeredChatHost
{
    /// <summary>
    /// Starts a fluent build. Prefer this over calling <see cref="LayeredChatOrchestrator"/>'s constructor directly.
    /// </summary>
    public static LayeredChatHostBuilder CreateBuilder() => new();

    internal LayeredChatHost(LayeredChatOrchestrator orchestrator)
    {
        Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <summary>
    /// Underlying orchestrator for advanced scenarios (same instance as used by run methods).
    /// </summary>
    public LayeredChatOrchestrator Orchestrator { get; }

    /// <inheritdoc cref="LayeredChatOrchestrator.RunTurnAsync" />
    public Task<LayeredChatTurnResult> RunTurnAsync(
        LayeredChatTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        return Orchestrator.RunTurnAsync(request, cancellationToken);
    }

    /// <inheritdoc cref="LayeredChatOrchestrator.RunTurnStreamingAsync" />
    public async IAsyncEnumerable<OrchestrationStreamEnvelope> RunTurnStreamingAsync(
        LayeredChatTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var envelope in Orchestrator.RunTurnStreamingAsync(request, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return envelope;
        }
    }

    /// <summary>
    /// Creates a named agent bound to this host’s orchestrator and the given registry key.
    /// </summary>
    public IChatAgent CreateAgent(string orchestrationRegistryKey)
    {
        return new LayeredChatAgent(Orchestrator, orchestrationRegistryKey);
    }
}
