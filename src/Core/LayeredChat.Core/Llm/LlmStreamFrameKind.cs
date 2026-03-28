namespace LayeredChat;

/// <summary>
/// Incremental frame emitted by <see cref="IStreamingLlmChatConnector"/> while a completion is in flight.
/// </summary>
public enum LlmStreamFrameKind
{
    TextDelta,
    ToolCallMeta,
    ToolArgumentsDelta,
    Usage,
    Completed
}
