using System.Text;

namespace LayeredChat;

/// <summary>
/// Collapses streaming frames into a single <see cref="LlmCompletionResult"/> for tool-loop logic.
/// </summary>
public sealed class LlmStreamAccumulatorSession
{
    private readonly StringBuilder _text = new();
    private readonly Dictionary<int, ToolAccumulator> _byIndex = new();
    private int _inputTokens;
    private int _outputTokens;

    private sealed class ToolAccumulator
    {
        public string? CallId { get; set; }

        public string? Name { get; set; }

        public StringBuilder Arguments { get; } = new();
    }

    public void Accept(LlmStreamFrame frame)
    {
        switch (frame.Kind)
        {
            case LlmStreamFrameKind.TextDelta:
                if (!string.IsNullOrEmpty(frame.TextDelta))
                {
                    _text.Append(frame.TextDelta);
                }

                break;
            case LlmStreamFrameKind.ToolCallMeta:
            {
                var idx = frame.ToolIndex ?? 0;
                if (!_byIndex.TryGetValue(idx, out var acc))
                {
                    acc = new ToolAccumulator();
                    _byIndex[idx] = acc;
                }

                if (!string.IsNullOrEmpty(frame.ToolCallId))
                {
                    acc.CallId = frame.ToolCallId;
                }

                if (!string.IsNullOrEmpty(frame.ToolName))
                {
                    acc.Name = frame.ToolName;
                }

                break;
            }
            case LlmStreamFrameKind.ToolArgumentsDelta:
            {
                var idx = frame.ToolIndex ?? 0;
                if (!_byIndex.TryGetValue(idx, out var acc))
                {
                    acc = new ToolAccumulator();
                    _byIndex[idx] = acc;
                }

                if (!string.IsNullOrEmpty(frame.ToolArgumentsDelta))
                {
                    acc.Arguments.Append(frame.ToolArgumentsDelta);
                }

                break;
            }
            case LlmStreamFrameKind.Usage:
                if (frame.InputTokens is { } i)
                {
                    _inputTokens = i;
                }

                if (frame.OutputTokens is { } o)
                {
                    _outputTokens = o;
                }

                break;
            case LlmStreamFrameKind.Completed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(frame), frame.Kind, "Unknown stream frame kind.");
        }
    }

    public LlmCompletionResult Build()
    {
        var toolCalls = new List<ToolCallRequest>();
        foreach (var kv in _byIndex.OrderBy(k => k.Key))
        {
            var acc = kv.Value;
            var name = acc.Name ?? string.Empty;
            if (string.IsNullOrEmpty(name) && acc.Arguments.Length == 0 && string.IsNullOrEmpty(acc.CallId))
            {
                continue;
            }

            toolCalls.Add(new ToolCallRequest
            {
                CallId = acc.CallId ?? Guid.NewGuid().ToString("N"),
                Name = name,
                ArgumentsJson = acc.Arguments.Length > 0 ? acc.Arguments.ToString() : "{}"
            });
        }

        return new LlmCompletionResult
        {
            TextContent = _text.Length > 0 ? _text.ToString() : null,
            ToolCalls = toolCalls,
            InputTokens = _inputTokens,
            OutputTokens = _outputTokens
        };
    }
}

/// <summary>
/// Helpers for stream aggregation.
/// </summary>
public static class LlmStreamAccumulator
{
    public static async Task<LlmCompletionResult> AccumulateAsync(
        IAsyncEnumerable<LlmStreamFrame> frames,
        Action<LlmStreamFrame>? onFrame = null,
        CancellationToken cancellationToken = default)
    {
        var session = new LlmStreamAccumulatorSession();
        await foreach (var frame in frames.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            onFrame?.Invoke(frame);
            session.Accept(frame);
        }

        return session.Build();
    }
}
