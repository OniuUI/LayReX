namespace LayeredChat;

/// <summary>
/// Named agent view over <see cref="LayeredChatOrchestrator"/> with a fixed orchestration registry key (manifest + tools + data sources).
/// </summary>
public interface IChatAgent
{
    string OrchestrationRegistryKey { get; }

    Task<LayeredChatTurnResult> RunTurnAsync(AgentTurnInput input, CancellationToken cancellationToken = default);

    IAsyncEnumerable<OrchestrationStreamEnvelope> RunTurnStreamingAsync(
        AgentTurnInput input,
        CancellationToken cancellationToken = default);
}
