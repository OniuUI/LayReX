using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LayeredChat.Connectors.OpenAiCompatible;

namespace LayeredChat.Tests;

public sealed class OpenAiCompatibleChatConnectorRequestTests
{
    [Fact]
    public async Task CompleteAsync_UsesMaxCompletionTokens_ForClaudeModels()
    {
        var handler = new CaptureHandler();
        var httpClient = new HttpClient(handler);
        var sut = new OpenAiCompatibleChatConnector(httpClient, new OpenAiCompatibleOptions
        {
            BaseUri = new Uri("https://example.com/v1/"),
            Model = "claude-latest"
        });
        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = "hello" } };

        await sut.CompleteAsync(messages, [], new LlmRequestOptions { MaxOutputTokens = 256 });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("max_completion_tokens", out var maxCompletionTokens));
        Assert.Equal(256, maxCompletionTokens.GetInt32());
        Assert.False(root.TryGetProperty("max_tokens", out _));
    }

    [Fact]
    public async Task CompleteAsync_UsesMaxTokens_ForNonClaudeModels()
    {
        var handler = new CaptureHandler();
        var httpClient = new HttpClient(handler);
        var sut = new OpenAiCompatibleChatConnector(httpClient, new OpenAiCompatibleOptions
        {
            BaseUri = new Uri("https://example.com/v1/"),
            Model = "gpt-4o-mini"
        });
        var messages = new[] { new ChatMessage { Role = ChatRole.User, Content = "hello" } };

        await sut.CompleteAsync(messages, [], new LlmRequestOptions { MaxOutputTokens = 128 });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("max_tokens", out var maxTokens));
        Assert.Equal(128, maxTokens.GetInt32());
        Assert.False(root.TryGetProperty("max_completion_tokens", out _));
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var responseJson = """
                               {"choices":[{"message":{"content":"ok"}}],"usage":{"prompt_tokens":1,"completion_tokens":1}}
                               """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
