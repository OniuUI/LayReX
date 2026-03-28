namespace LayeredChat;

/// <summary>
/// One user turn: prior transcript (without system), new user text, base system instruction from the host, and target orchestration key.
/// </summary>
public sealed class LayeredChatTurnRequest
{
    public string OrchestrationRegistryKey { get; init; } = string.Empty;

    public IReadOnlyList<ChatMessage> PriorMessages { get; init; } = [];

    public string UserMessageContent { get; init; } = string.Empty;

    public string SystemInstructionText { get; init; } = string.Empty;

    public OrchestrationSessionContext Session { get; init; } = new();

    public LlmRequestOptions? ConnectorOptions { get; init; }

    public OrchestrationExecutionHooks? Hooks { get; init; }

    public LlmModelAdapterProfile? ModelAdapterProfile { get; init; }
}
