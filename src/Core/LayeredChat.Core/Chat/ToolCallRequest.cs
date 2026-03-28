namespace LayeredChat;

/// <summary>
/// A tool invocation requested by the model.
/// </summary>
public sealed class ToolCallRequest
{
    public string CallId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = "{}";
}
