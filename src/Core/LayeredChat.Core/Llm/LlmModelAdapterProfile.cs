namespace LayeredChat;

/// <summary>
/// Describes provider-specific behavior hints so hosts can tune requests (tool formats, streaming quirks) per deployed LLM integration.
/// </summary>
public sealed class LlmModelAdapterProfile
{
    public string ProfileId { get; init; } = "default";

    public bool SupportsStreaming { get; init; } = true;

    public bool SupportsParallelToolCalls { get; init; } = true;

    /// <summary>Provider supports native JSON-schema constrained output (OpenAI json_schema, Gemini responseSchema).</summary>
    public bool SupportsNativeJsonSchema { get; init; } = true;

    /// <summary>Provider supports forcing a specific tool choice (used by <see cref="ResponseSchemaMode.ForcedTool"/>).</summary>
    public bool SupportsForcedToolChoice { get; init; } = true;

    public int? DefaultMaxOutputTokens { get; init; }

    public string? ReasoningEffortHint { get; init; }
}
