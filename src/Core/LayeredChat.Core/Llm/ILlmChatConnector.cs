namespace LayeredChat;

/// <summary>
/// Pluggable connector to any .NET-accessible chat model (OpenAI, Anthropic, Azure OpenAI, Google, Ollama, etc.).
/// Implementations live in host or adapter packages; Core stays provider-agnostic.
/// </summary>
public interface ILlmChatConnector
{
    /// <summary>
    /// Provider label for logging and capability discovery (e.g. OpenAICompatible).
    /// </summary>
    string ConnectorKind { get; }

    /// <summary>
    /// Runs one completion step with optional tools. The host appends assistant and tool messages for multi-step loops.
    /// </summary>
    Task<LlmCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        CancellationToken cancellationToken = default);
}
