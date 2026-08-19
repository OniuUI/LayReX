namespace LayeredChat.Connectors.AnthropicNative;

public sealed class AnthropicNativeOptions
{
    public Uri BaseUri { get; init; } = new("https://api.anthropic.com");

    public string Model { get; init; } = "claude-sonnet-4-20250514";

    public string? ApiKey { get; init; }

    public string AnthropicVersion { get; init; } = "2023-06-01";

    public string MessagesPath { get; init; } = "v1/messages";

    /// <summary>
    /// When true, requests include Anthropic <c>cache_control</c> on the first system block and the last tool.
    /// Per-request <see cref="LlmRequestOptions.EnablePromptCache"/> also enables this.
    /// </summary>
    public bool EnablePromptCache { get; init; }
}
