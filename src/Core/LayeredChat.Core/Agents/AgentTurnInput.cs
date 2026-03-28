namespace LayeredChat;

/// <summary>
/// User turn input for <see cref="IChatAgent"/> without an orchestration registry key; the agent supplies the key.
/// </summary>
public sealed class AgentTurnInput
{
    public IReadOnlyList<ChatMessage> PriorMessages { get; init; } = [];

    public string UserMessageContent { get; init; } = string.Empty;

    public string SystemInstructionText { get; init; } = string.Empty;

    public OrchestrationSessionContext Session { get; init; } = new();

    public LlmRequestOptions? ConnectorOptions { get; init; }

    public OrchestrationExecutionHooks? Hooks { get; init; }

    public LlmModelAdapterProfile? ModelAdapterProfile { get; init; }
}
