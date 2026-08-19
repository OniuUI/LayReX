using System.Net;
using System.Text;
using System.Text.Json;
using LayeredChat.Connectors.AnthropicNative;

namespace LayeredChat.Tests;

public sealed class AnthropicNativeChatConnectorCacheTests
{
    [Fact]
    public async Task CompleteAsync_WhenPromptCacheEnabled_MarksStableSystemAndLastTool()
    {
        var handler = new CaptureHandler();
        var httpClient = new HttpClient(handler);
        var sut = new AnthropicNativeChatConnector(httpClient, new AnthropicNativeOptions
        {
            BaseUri = new Uri("https://example.com"),
            Model = "claude-sonnet-4-20250514",
            EnablePromptCache = true
        });
        var messages = new[]
        {
            new ChatMessage { Role = ChatRole.System, Content = "STABLE PREFIX" },
            new ChatMessage { Role = ChatRole.System, Content = "volatile turn" },
            new ChatMessage { Role = ChatRole.User, Content = "hello" }
        };
        var tools = new[]
        {
            new ToolDefinition { Name = "alpha", Description = "a", ParametersSchemaJson = """{"type":"object"}""" },
            new ToolDefinition { Name = "beta", Description = "b", ParametersSchemaJson = """{"type":"object"}""" }
        };

        var result = await sut.CompleteAsync(
            messages,
            tools,
            new LlmRequestOptions { MaxOutputTokens = 128, EnablePromptCache = true });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var root = doc.RootElement;
        var system = root.GetProperty("system");
        Assert.Equal(JsonValueKind.Array, system.ValueKind);
        Assert.Equal(2, system.GetArrayLength());
        var cached = system[0];
        Assert.Equal("STABLE PREFIX", cached.GetProperty("text").GetString());
        Assert.Equal("ephemeral", cached.GetProperty("cache_control").GetProperty("type").GetString());
        var volatileBlock = system[1];
        Assert.Equal("volatile turn", volatileBlock.GetProperty("text").GetString());
        Assert.False(volatileBlock.TryGetProperty("cache_control", out _));

        var toolArray = root.GetProperty("tools");
        Assert.False(toolArray[0].TryGetProperty("cache_control", out _));
        Assert.Equal("ephemeral", toolArray[1].GetProperty("cache_control").GetProperty("type").GetString());

        Assert.Equal(40, result.CacheCreationInputTokens);
        Assert.Equal(900, result.CacheReadInputTokens);
    }

    [Fact]
    public async Task CompleteAsync_WhenPromptCacheDisabled_KeepsPlainSystemString()
    {
        var handler = new CaptureHandler();
        var httpClient = new HttpClient(handler);
        var sut = new AnthropicNativeChatConnector(httpClient, new AnthropicNativeOptions
        {
            BaseUri = new Uri("https://example.com"),
            Model = "claude-sonnet-4-20250514"
        });
        var messages = new[]
        {
            new ChatMessage { Role = ChatRole.System, Content = "one" },
            new ChatMessage { Role = ChatRole.User, Content = "hello" }
        };

        await sut.CompleteAsync(messages, [], new LlmRequestOptions { MaxOutputTokens = 32 });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("system").ValueKind);
        Assert.Equal("one", doc.RootElement.GetProperty("system").GetString());
    }

    [Fact]
    public async Task CompleteAsync_FailedToolResult_SetsIsError()
    {
        var handler = new CaptureHandler();
        var httpClient = new HttpClient(handler);
        var sut = new AnthropicNativeChatConnector(httpClient, new AnthropicNativeOptions
        {
            BaseUri = new Uri("https://example.com"),
            Model = "claude-sonnet-4-20250514"
        });
        var messages = new[]
        {
            new ChatMessage { Role = ChatRole.User, Content = "go" },
            new ChatMessage
            {
                Role = ChatRole.Tool,
                ToolCallId = "toolu_1",
                Content = """{"status":"denied"}""",
                IsError = true
            }
        };

        await sut.CompleteAsync(messages, [], new LlmRequestOptions { MaxOutputTokens = 32 });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var content = doc.RootElement.GetProperty("messages")[1].GetProperty("content")[0];
        Assert.Equal("tool_result", content.GetProperty("type").GetString());
        Assert.True(content.GetProperty("is_error").GetBoolean());
    }

    [Fact]
    public async Task CompleteAsync_ToolResultWithoutToolCallId_Throws()
    {
        var handler = new CaptureHandler();
        var httpClient = new HttpClient(handler);
        var sut = new AnthropicNativeChatConnector(httpClient, new AnthropicNativeOptions
        {
            BaseUri = new Uri("https://example.com"),
            Model = "claude-sonnet-4-20250514"
        });
        var messages = new[]
        {
            new ChatMessage { Role = ChatRole.User, Content = "go" },
            new ChatMessage { Role = ChatRole.Tool, Content = "missing id" }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteAsync(messages, [], new LlmRequestOptions { MaxOutputTokens = 32 }));
        Assert.Equal(string.Empty, handler.LastRequestBody);
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
                               {"content":[{"type":"text","text":"ok"}],"usage":{"input_tokens":10,"output_tokens":2,"cache_creation_input_tokens":40,"cache_read_input_tokens":900}}
                               """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
