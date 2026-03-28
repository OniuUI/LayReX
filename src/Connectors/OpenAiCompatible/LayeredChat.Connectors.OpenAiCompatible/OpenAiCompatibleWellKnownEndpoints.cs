namespace LayeredChat.Connectors.OpenAiCompatible;

/// <summary>
/// Pre-configured <see cref="OpenAiCompatibleOptions"/> for common OpenAI-compatible entry points.
/// Hosts may still use custom <see cref="Uri"/> and paths for less common deployments.
/// </summary>
public static class OpenAiCompatibleWellKnownEndpoints
{
    private const string DefaultChatPath = "chat/completions";

    /// <summary>
    /// OpenAI public API (default shape).
    /// </summary>
    public static OpenAiCompatibleOptions OpenAI(string apiKey, string model) =>
        new()
        {
            BaseUri = new Uri("https://api.openai.com/v1/"),
            ApiKey = apiKey,
            Model = model,
            ChatCompletionsPath = DefaultChatPath
        };

    /// <summary>
    /// Google Gemini when using the OpenAI-compatible surface (verify tool and streaming support for your model).
    /// </summary>
    public static OpenAiCompatibleOptions GoogleGeminiOpenAiCompatible(string apiKey, string model) =>
        new()
        {
            BaseUri = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"),
            ApiKey = apiKey,
            Model = model,
            ChatCompletionsPath = DefaultChatPath
        };

    /// <summary>
    /// Local Ollama OpenAI-compatible server.
    /// </summary>
    public static OpenAiCompatibleOptions Ollama(Uri baseUri, string model, string? apiKey = null) =>
        new()
        {
            BaseUri = NormalizeV1Base(baseUri),
            ApiKey = apiKey,
            Model = model,
            ChatCompletionsPath = DefaultChatPath
        };

    /// <summary>
    /// LiteLLM or other gateway exposing OpenAI-style chat under a custom base.
    /// </summary>
    public static OpenAiCompatibleOptions FromGatewayBase(Uri openAiCompatibleV1Base, string apiKey, string model) =>
        new()
        {
            BaseUri = NormalizeV1Base(openAiCompatibleV1Base),
            ApiKey = string.IsNullOrEmpty(apiKey) ? null : apiKey,
            Model = model,
            ChatCompletionsPath = DefaultChatPath
        };

    private static Uri NormalizeV1Base(Uri baseUri)
    {
        var s = baseUri.ToString().TrimEnd('/');
        if (!s.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            s = s + "/v1";
        }

        return new Uri(s + "/", UriKind.Absolute);
    }
}
