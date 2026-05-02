using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredChat.Connectors.OpenAiCompatible;

/// <summary>
/// Calls OpenAI-style <c>/v1/chat/completions</c> over HTTP with JSON tools and optional SSE streaming.
/// </summary>
public sealed class OpenAiCompatibleChatConnector : IStreamingLlmChatConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly OpenAiCompatibleOptions _options;

    public OpenAiCompatibleChatConnector(HttpClient http, OpenAiCompatibleOptions options, string connectorKind = "OpenAiCompatible")
    {
        _http = http;
        _options = options;
        ConnectorKind = connectorKind;
    }

    public string ConnectorKind { get; }

    public async Task<LlmCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        var model = options.ModelNameOverride ?? _options.Model;
        var body = BuildRequestBody(model, messages, tools, options, stream: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri());
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        AddAuth(request);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessStatusCodeWithBodyAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ParseNonStreaming(doc.RootElement);
    }

    public async IAsyncEnumerable<LlmStreamFrame> CompleteStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var model = options.ModelNameOverride ?? _options.Model;
        var body = BuildRequestBody(model, messages, tools, options, stream: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri());
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        AddAuth(request);

        using var response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessStatusCodeWithBodyAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line["data:".Length..].Trim();
            if (payload == "[DONE]")
            {
                break;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(payload);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                foreach (var frame in ParseStreamChunk(doc.RootElement))
                {
                    yield return frame;
                }
            }
        }

        yield return new LlmStreamFrame { Kind = LlmStreamFrameKind.Completed };
    }

    private Uri BuildUri()
    {
        var baseUri = _options.BaseUri.ToString().TrimEnd('/');
        var path = _options.ChatCompletionsPath.TrimStart('/');
        return new Uri($"{baseUri}/{path}");
    }

    private void AddAuth(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private static object BuildRequestBody(
        string model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        bool stream)
    {
        var msgObjs = new List<object>(messages.Count);
        foreach (var message in messages)
        {
            msgObjs.Add(MapMessage(message));
        }

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = msgObjs,
            ["temperature"] = options.Temperature,
            ["stream"] = stream
        };

        if (options.MaxOutputTokens is { } max)
        {
            body[UseMaxCompletionTokensField(model) ? "max_completion_tokens" : "max_tokens"] = max;
        }

        if (tools.Count > 0)
        {
            var toolObjs = new List<object>(tools.Count);
            foreach (var t in tools)
            {
                toolObjs.Add(new Dictionary<string, object?>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = t.Name,
                        ["description"] = t.Description,
                        ["parameters"] = t.ResolveParametersElement()
                    }
                });
            }

            body["tools"] = toolObjs;
        }

        return body;
    }

    private static bool UseMaxCompletionTokensField(string model) =>
        !string.IsNullOrWhiteSpace(model)
        && model.Contains("claude", StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureSuccessStatusCodeWithBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = response.Content == null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        body = string.IsNullOrWhiteSpace(body) ? "(empty response body)" : body.Trim();
        throw new HttpRequestException(
            $"Response status code {(int)response.StatusCode} ({response.ReasonPhrase}) from OpenAI-compatible connector. Body: {body}",
            null,
            response.StatusCode);
    }

    private static object MapMessage(ChatMessage m)
    {
        switch (m.Role)
        {
            case ChatRole.System:
            case ChatRole.User:
                return new Dictionary<string, object?> { ["role"] = m.Role.ToString().ToLowerInvariant(), ["content"] = m.Content };
            case ChatRole.Assistant:
            {
                var d = new Dictionary<string, object?> { ["role"] = "assistant" };
                if (!string.IsNullOrEmpty(m.Content))
                {
                    d["content"] = m.Content;
                }

                if (m.ToolCalls is { Count: > 0 } tc)
                {
                    var toolCallObjs = new List<object>(tc.Count);
                    foreach (var c in tc)
                    {
                        toolCallObjs.Add(new Dictionary<string, object?>
                        {
                            ["id"] = c.CallId,
                            ["type"] = "function",
                            ["function"] = new Dictionary<string, object?>
                            {
                                ["name"] = c.Name,
                                ["arguments"] = string.IsNullOrWhiteSpace(c.ArgumentsJson) ? "{}" : c.ArgumentsJson
                            }
                        });
                    }

                    d["tool_calls"] = toolCallObjs;
                }

                return d;
            }
            case ChatRole.Tool:
                return new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = m.ToolCallId,
                    ["content"] = m.Content
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(m), m.Role, null);
        }
    }

    private static LlmCompletionResult ParseNonStreaming(JsonElement root)
    {
        var text = string.Empty;
        var toolCalls = new List<ToolCallRequest>();
        var input = 0;
        var output = 0;

        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var pt))
            {
                input = pt.GetInt32();
            }

            if (usage.TryGetProperty("completion_tokens", out var ct))
            {
                output = ct.GetInt32();
            }
        }

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return new LlmCompletionResult
            {
                TextContent = string.IsNullOrEmpty(text) ? null : text,
                ToolCalls = toolCalls,
                InputTokens = input,
                OutputTokens = output
            };
        }

        var choice = choices[0];
        if (!choice.TryGetProperty("message", out var message))
        {
            return new LlmCompletionResult
            {
                TextContent = null,
                ToolCalls = toolCalls,
                InputTokens = input,
                OutputTokens = output
            };
        }

        if (message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
        {
            text = contentEl.GetString() ?? string.Empty;
        }

        if (message.TryGetProperty("tool_calls", out var tcEl) && tcEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in tcEl.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idP) ? idP.GetString() ?? string.Empty : string.Empty;
                if (!el.TryGetProperty("function", out var fn))
                {
                    continue;
                }

                var name = fn.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var args = fn.TryGetProperty("arguments", out var a)
                    ? a.GetString() ?? "{}"
                    : "{}";
                toolCalls.Add(new ToolCallRequest { CallId = id, Name = name, ArgumentsJson = args });
            }
        }

        return new LlmCompletionResult
        {
            TextContent = string.IsNullOrEmpty(text) ? null : text,
            ToolCalls = toolCalls,
            InputTokens = input,
            OutputTokens = output
        };
    }

    private static IEnumerable<LlmStreamFrame> ParseStreamChunk(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            yield break;
        }

        var choice = choices[0];
        if (!choice.TryGetProperty("delta", out var delta))
        {
            yield break;
        }

        if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
        {
            var s = c.GetString();
            if (!string.IsNullOrEmpty(s))
            {
                yield return new LlmStreamFrame { Kind = LlmStreamFrameKind.TextDelta, TextDelta = s };
            }
        }

        if (delta.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in tc.EnumerateArray())
            {
                var idx = el.TryGetProperty("index", out var ix) ? ix.GetInt32() : 0;
                if (el.TryGetProperty("id", out var idP))
                {
                    yield return new LlmStreamFrame
                    {
                        Kind = LlmStreamFrameKind.ToolCallMeta,
                        ToolIndex = idx,
                        ToolCallId = idP.GetString()
                    };
                }

                if (el.TryGetProperty("function", out var fn))
                {
                    if (fn.TryGetProperty("name", out var nameP))
                    {
                        yield return new LlmStreamFrame
                        {
                            Kind = LlmStreamFrameKind.ToolCallMeta,
                            ToolIndex = idx,
                            ToolName = nameP.GetString()
                        };
                    }

                    if (fn.TryGetProperty("arguments", out var argP) && argP.ValueKind == JsonValueKind.String)
                    {
                        var frag = argP.GetString();
                        if (!string.IsNullOrEmpty(frag))
                        {
                            yield return new LlmStreamFrame
                            {
                                Kind = LlmStreamFrameKind.ToolArgumentsDelta,
                                ToolIndex = idx,
                                ToolArgumentsDelta = frag
                            };
                        }
                    }
                }
            }
        }

        if (root.TryGetProperty("usage", out var usage))
        {
            int? i = null;
            int? o = null;
            if (usage.TryGetProperty("prompt_tokens", out var pt))
            {
                i = pt.GetInt32();
            }

            if (usage.TryGetProperty("completion_tokens", out var ct))
            {
                o = ct.GetInt32();
            }

            if (i is not null || o is not null)
            {
                yield return new LlmStreamFrame { Kind = LlmStreamFrameKind.Usage, InputTokens = i, OutputTokens = o };
            }
        }
    }
}
