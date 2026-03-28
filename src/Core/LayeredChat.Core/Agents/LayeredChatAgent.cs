using System.Runtime.CompilerServices;

namespace LayeredChat;

/// <summary>
/// Default <see cref="IChatAgent"/> binding one orchestration profile to the shared orchestrator.
/// </summary>
public sealed class LayeredChatAgent : IChatAgent
{
    private readonly LayeredChatOrchestrator _orchestrator;

    public LayeredChatAgent(LayeredChatOrchestrator orchestrator, string orchestrationRegistryKey)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestrationRegistryKey);
        OrchestrationRegistryKey = orchestrationRegistryKey;
    }

    public string OrchestrationRegistryKey { get; }

    public Task<LayeredChatTurnResult> RunTurnAsync(AgentTurnInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return _orchestrator.RunTurnAsync(ToRequest(input), cancellationToken);
    }

    public async IAsyncEnumerable<OrchestrationStreamEnvelope> RunTurnStreamingAsync(
        AgentTurnInput input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await foreach (var envelope in _orchestrator.RunTurnStreamingAsync(ToRequest(input), cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return envelope;
        }
    }

    private LayeredChatTurnRequest ToRequest(AgentTurnInput input)
    {
        return new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKey,
            PriorMessages = input.PriorMessages,
            UserMessageContent = input.UserMessageContent,
            SystemInstructionText = input.SystemInstructionText,
            Session = input.Session,
            ConnectorOptions = input.ConnectorOptions,
            Hooks = input.Hooks,
            ModelAdapterProfile = input.ModelAdapterProfile
        };
    }
}
