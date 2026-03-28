namespace LayeredChat;

/// <summary>
/// One streaming chunk from the model connector. Tool call metadata and argument deltas use <see cref="ToolIndex"/> to correlate fragments.
/// </summary>
public sealed class LlmStreamFrame
{
    public LlmStreamFrameKind Kind { get; init; }

    public string? TextDelta { get; init; }

    public int? ToolIndex { get; init; }

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    public string? ToolArgumentsDelta { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }
}
