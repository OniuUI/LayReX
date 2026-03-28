namespace LayeredChat.Connectors.OpenAiCompatible;

/// <summary>
/// Base URL and model for OpenAI-compatible HTTP APIs (OpenAI, Azure OpenAI path-style, Ollama, LM Studio, vLLM).
/// </summary>
public sealed class OpenAiCompatibleOptions
{
    public Uri BaseUri { get; init; } = new("https://api.openai.com/v1/");

    public string Model { get; init; } = "gpt-4o-mini";

    public string? ApiKey { get; init; }

    public string ChatCompletionsPath { get; init; } = "chat/completions";
}
