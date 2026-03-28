using LayeredChat.Connectors.OpenAiCompatible;

namespace LayeredChat.Tests;

public sealed class OpenAiCompatibleWellKnownEndpointsTests
{
    [Fact]
    public void GoogleGeminiOpenAiCompatible_uses_expected_host()
    {
        var o = OpenAiCompatibleWellKnownEndpoints.GoogleGeminiOpenAiCompatible("key", "gemini-2.0-flash");
        Assert.Contains("generativelanguage.googleapis.com", o.BaseUri.Host, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("gemini-2.0-flash", o.Model);
        Assert.Equal("key", o.ApiKey);
    }

    [Fact]
    public void Ollama_appends_v1_segment()
    {
        var o = OpenAiCompatibleWellKnownEndpoints.Ollama(new Uri("http://localhost:11434"), "llama");
        Assert.True(o.BaseUri.ToString().Contains("/v1/", StringComparison.OrdinalIgnoreCase));
    }
}
