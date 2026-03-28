namespace LayeredChat;

/// <summary>
/// Text block merged into the model context (system or user) by the context pipeline.
/// </summary>
public sealed class ContextSlice
{
    public string Label { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public int? SuggestedTokenBudget { get; init; }
}
