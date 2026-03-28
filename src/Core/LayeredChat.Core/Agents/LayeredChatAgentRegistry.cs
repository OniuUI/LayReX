namespace LayeredChat;

/// <summary>
/// Registers multiple <see cref="IChatAgent"/> instances (different orchestration keys) over one <see cref="LayeredChatOrchestrator"/>.
/// </summary>
public sealed class LayeredChatAgentRegistry
{
    private readonly LayeredChatOrchestrator _orchestrator;
    private readonly Dictionary<string, IChatAgent> _agents = new(StringComparer.OrdinalIgnoreCase);

    public LayeredChatAgentRegistry(LayeredChatOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public void Register(string agentId, string orchestrationRegistryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(orchestrationRegistryKey);
        _agents[agentId] = new LayeredChatAgent(_orchestrator, orchestrationRegistryKey);
    }

    public IChatAgent Get(string agentId)
    {
        if (!_agents.TryGetValue(agentId, out var agent) || agent is null)
        {
            throw new KeyNotFoundException($"No agent registered for id '{agentId}'.");
        }

        return agent;
    }

    public bool TryGet(string agentId, out IChatAgent? agent)
    {
        return _agents.TryGetValue(agentId, out agent);
    }
}
