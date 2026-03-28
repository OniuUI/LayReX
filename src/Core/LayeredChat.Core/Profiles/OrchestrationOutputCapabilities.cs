namespace LayeredChat;

/// <summary>
/// Declares how the host may surface orchestration results to clients (text, tools, structured payloads, streaming).
/// </summary>
[Flags]
public enum OrchestrationOutputCapabilities
{
    None = 0,
    TextReply = 1 << 0,
    ToolCalls = 1 << 1,
    StructuredJson = 1 << 2,
    BinaryPayload = 1 << 3,
    ServerSentEvents = 1 << 4
}
