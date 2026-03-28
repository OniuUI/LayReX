namespace LayeredChat;

/// <summary>
/// Per-request options passed to <see cref="ILlmChatConnector"/>. Connectors map these to provider-specific APIs.
/// </summary>
public sealed class LlmRequestOptions
{
    public double Temperature { get; init; } = 0.2;

    public int? MaxOutputTokens { get; init; }

    public int? MaxToolRoundIterations { get; init; }

    public string? ModelNameOverride { get; init; }

    public LlmModelAdapterProfile? AdapterProfile { get; init; }
}
