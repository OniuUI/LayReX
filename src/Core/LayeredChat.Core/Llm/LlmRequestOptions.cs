namespace LayeredChat;

/// <summary>
/// Per-request options passed to <see cref="ILlmChatConnector"/>. Connectors map these to provider-specific APIs.
/// </summary>
public sealed class LlmRequestOptions
{
    public double Temperature { get; init; } = 0.2;

    /// <summary>
    /// When set, connectors map this to the provider completion token limit (e.g. OpenAI <c>max_tokens</c>).
    /// Leave unset to omit the field and use the provider default (often too low for long tool-heavy turns).
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    public int? MaxToolRoundIterations { get; init; }

    public string? ModelNameOverride { get; init; }

    public LlmModelAdapterProfile? AdapterProfile { get; init; }

    /// <summary>
    /// When set, the model's final answer is schema-constrained. Connectors map this to the strongest
    /// mechanism the provider supports; the orchestrator folds <see cref="ResponseSchemaSpec.EmitToolName"/>
    /// tool calls back into text content.
    /// </summary>
    public ResponseSchemaSpec? ResponseSchema { get; init; }

    /// <summary>
    /// Orchestrator stream envelope volume when <see cref="LayeredChatTurnRequest.Hooks"/> telemetry is attached.
    /// </summary>
    public OrchestrationTelemetryVerbosity TelemetryVerbosity { get; init; } = OrchestrationTelemetryVerbosity.Normal;

    /// <summary>
    /// When true, connectors that support provider prompt caching (Anthropic <c>cache_control</c>)
    /// mark the stable system prefix and the last tool definition as cache breakpoints.
    /// </summary>
    public bool EnablePromptCache { get; init; }
}
