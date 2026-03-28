namespace LayeredChat;

/// <summary>
/// Optional streaming surface for connectors that support token or chunk streaming. Hosts fall back to <see cref="ILlmChatConnector.CompleteAsync"/> when not implemented.
/// </summary>
public interface IStreamingLlmChatConnector : ILlmChatConnector
{
    IAsyncEnumerable<LlmStreamFrame> CompleteStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        CancellationToken cancellationToken = default);
}
