using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredChat.Connectors.AnthropicNative;

public sealed class AnthropicNativeChatConnector : IStreamingLlmChatConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly AnthropicNativeOptions _options;

    public AnthropicNativeChatConnector(HttpClient http, AnthropicNativeOptions options)
    {
        _http = http;
        _options = options;
    }

    public string ConnectorKind => "AnthropicNative";

    public async Task<LlmCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        var model = options.ModelNameOverride ?? _options.Model;
        var body = BuildRequestBody(model, messages, tools, options, stream: false, enablePromptCache: PromptCacheEnabled(options));
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
        var body = BuildRequestBody(model, messages, tools, options, stream: true, enablePromptCache: PromptCacheEnabled(options));
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

        var currentEvent = string.Empty;
        var blockIndex = 0;
        var streamedToolIndex = -1;
        var toolCallIds = new Dictionary<int, string>();
        var toolCallNames = new Dictionary<int, string>();
        var inputTokens = 0;
        var outputTokens = 0;
        var cacheCreationTokens = 0;
        var cacheReadTokens = 0;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
                break;

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                currentEvent = line["event:".Length..].Trim();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var payload = line["data:".Length..].Trim();
            if (string.IsNullOrEmpty(payload))
                continue;

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
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeEl))
                {
                    var type = typeEl.GetString();

                    switch (type)
                    {
                        case "message_start":
                            if (root.TryGetProperty("message", out var msg) &&
                                msg.TryGetProperty("usage", out var usageStart))
                            {
                                ReadTokenUsage(usageStart, out inputTokens, out _, out cacheCreationTokens, out cacheReadTokens);
                                yield return new LlmStreamFrame
                                {
                                    Kind = LlmStreamFrameKind.Usage,
                                    InputTokens = inputTokens,
                                    OutputTokens = null,
                                    CacheCreationInputTokens = cacheCreationTokens,
                                    CacheReadInputTokens = cacheReadTokens
                                };
                            }
                            break;

                        case "content_block_start":
                            if (root.TryGetProperty("index", out var idxEl))
                                blockIndex = idxEl.GetInt32();
                            if (root.TryGetProperty("content_block", out var block))
                            {
                                if (block.TryGetProperty("type", out var bt) && bt.GetString() == "tool_use")
                                {
                                    streamedToolIndex++;
                                    var actualToolIdx = streamedToolIndex;
                                    if (block.TryGetProperty("id", out var tid))
                                    {
                                        toolCallIds[actualToolIdx] = tid.GetString() ?? string.Empty;
                                        yield return new LlmStreamFrame
                                        {
                                            Kind = LlmStreamFrameKind.ToolCallMeta,
                                            ToolIndex = actualToolIdx,
                                            ToolCallId = tid.GetString()
                                        };
                                    }
                                    if (block.TryGetProperty("name", out var tn))
                                    {
                                        toolCallNames[actualToolIdx] = tn.GetString() ?? string.Empty;
                                        yield return new LlmStreamFrame
                                        {
                                            Kind = LlmStreamFrameKind.ToolCallMeta,
                                            ToolIndex = actualToolIdx,
                                            ToolName = tn.GetString()
                                        };
                                    }
                                }
                            }
                            break;

                        case "content_block_delta":
                            if (root.TryGetProperty("delta", out var delta))
                            {
                                var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : null;
                                if (deltaType == "text_delta" && delta.TryGetProperty("text", out var tt))
                                {
                                    var text = tt.GetString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        yield return new LlmStreamFrame
                                        {
                                            Kind = LlmStreamFrameKind.TextDelta,
                                            TextDelta = text
                                        };
                                    }
                                }
                                else if (deltaType == "input_json_delta" && delta.TryGetProperty("partial_json", out var pj))
                                {
                                    var frag = pj.GetString();
                                    if (!string.IsNullOrEmpty(frag))
                                    {
                                        yield return new LlmStreamFrame
                                        {
                                            Kind = LlmStreamFrameKind.ToolArgumentsDelta,
                                            ToolIndex = streamedToolIndex,
                                            ToolArgumentsDelta = frag
                                        };
                                    }
                                }
                            }
                            break;

                        case "message_delta":
                            if (root.TryGetProperty("usage", out var usageDelta))
                            {
                                ReadTokenUsage(usageDelta, out _, out outputTokens, out var deltaCacheCreate, out var deltaCacheRead);
                                if (deltaCacheCreate > 0)
                                    cacheCreationTokens = deltaCacheCreate;
                                if (deltaCacheRead > 0)
                                    cacheReadTokens = deltaCacheRead;
                                yield return new LlmStreamFrame
                                {
                                    Kind = LlmStreamFrameKind.Usage,
                                    InputTokens = inputTokens,
                                    OutputTokens = outputTokens,
                                    CacheCreationInputTokens = cacheCreationTokens,
                                    CacheReadInputTokens = cacheReadTokens
                                };
                            }
                            break;

                        case "message_stop":
                            break;
                    }
                }
            }
        }

        yield return new LlmStreamFrame { Kind = LlmStreamFrameKind.Completed };
    }

    private Uri BuildUri()
    {
        var baseUri = _options.BaseUri.ToString().TrimEnd('/');
        var path = _options.MessagesPath.TrimStart('/');
        return new Uri($"{baseUri}/{path}");
    }

    private void AddAuth(HttpRequestMessage request)
    {
        var apiKey = _options.ApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
            return;

        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", _options.AnthropicVersion);
    }

    private bool PromptCacheEnabled(LlmRequestOptions options) =>
        options.EnablePromptCache || _options.EnablePromptCache;

    private static Dictionary<string, object?> CacheControlEphemeral() =>
        new() { ["type"] = "ephemeral" };

    private static object BuildRequestBody(
        string model,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        bool stream,
        bool enablePromptCache)
    {
        var systemParts = new List<string>();
        var anthropicMessages = new List<object>(messages.Count);

        foreach (var m in messages)
        {
            if (m.Role == ChatRole.System)
            {
                if (!string.IsNullOrEmpty(m.Content))
                    systemParts.Add(m.Content);
                continue;
            }

            switch (m.Role)
            {
                case ChatRole.User:
                    anthropicMessages.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["content"] = m.Content
                    });
                    break;

                case ChatRole.Assistant:
                {
                    if (m.ToolCalls is { Count: > 0 } tc)
                    {
                        var contentBlocks = new List<object>(tc.Count + 1);
                        if (!string.IsNullOrEmpty(m.Content))
                        {
                            contentBlocks.Add(new Dictionary<string, object?>
                            {
                                ["type"] = "text",
                                ["text"] = m.Content
                            });
                        }
                        foreach (var c in tc)
                        {
                            JsonElement parsedInput;
                            try
                            {
                                using var argDoc = JsonDocument.Parse(
                                    string.IsNullOrWhiteSpace(c.ArgumentsJson) ? "{}" : c.ArgumentsJson);
                                parsedInput = argDoc.RootElement.Clone();
                            }
                            catch (JsonException)
                            {
                                parsedInput = JsonDocument.Parse("{}").RootElement.Clone();
                            }

                            contentBlocks.Add(new Dictionary<string, object?>
                            {
                                ["type"] = "tool_use",
                                ["id"] = c.CallId,
                                ["name"] = c.Name,
                                ["input"] = parsedInput
                            });
                        }

                        anthropicMessages.Add(new Dictionary<string, object?>
                        {
                            ["role"] = "assistant",
                            ["content"] = contentBlocks
                        });
                    }
                    else
                    {
                        anthropicMessages.Add(new Dictionary<string, object?>
                        {
                            ["role"] = "assistant",
                            ["content"] = m.Content ?? string.Empty
                        });
                    }

                    break;
                }

                case ChatRole.Tool:
                {
                    var toolResult = new Dictionary<string, object?>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = m.ToolCallId ?? string.Empty,
                        ["content"] = m.Content ?? string.Empty
                    };
                    if (m.IsError)
                        toolResult["is_error"] = true;

                    anthropicMessages.Add(new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["content"] = new[] { toolResult }
                    });
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(messages), m.Role, null);
            }
        }

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["max_tokens"] = options.MaxOutputTokens ?? 4096,
            ["messages"] = anthropicMessages,
            ["stream"] = stream
        };

        var schemaMode = options.ResponseSchema?.ResolveMode(
            options.AdapterProfile, ResponseSchemaMode.ForcedTool);
        if (options.ResponseSchema is { } promptedSchema && schemaMode == ResponseSchemaMode.PromptedJson)
        {
            var instruction = "Respond ONLY with a single JSON object conforming to this JSON Schema. " +
                              "No markdown fences, no prose before or after.\n" +
                              promptedSchema.SchemaJson;
            systemParts.Add(instruction);
        }

        if (systemParts.Count > 0)
        {
            if (enablePromptCache)
            {
                var blocks = new List<object>(systemParts.Count);
                for (var i = 0; i < systemParts.Count; i++)
                {
                    var block = new Dictionary<string, object?>
                    {
                        ["type"] = "text",
                        ["text"] = systemParts[i]
                    };
                    if (i == 0)
                        block["cache_control"] = CacheControlEphemeral();
                    blocks.Add(block);
                }

                body["system"] = blocks;
            }
            else
            {
                body["system"] = string.Join("\n\n", systemParts);
            }
        }

        if (options.Temperature != 0.2)
        {
            body["temperature"] = options.Temperature;
        }

        var toolObjs = new List<object>(tools.Count + 1);
        foreach (var t in tools)
        {
            var inputSchema = t.ResolveParametersElement();
            toolObjs.Add(new Dictionary<string, object?>
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["input_schema"] = inputSchema
            });
        }

        if (options.ResponseSchema is { } schema &&
            schemaMode is ResponseSchemaMode.ForcedTool or ResponseSchemaMode.NativeJsonSchema)
        {
            toolObjs.Add(new Dictionary<string, object?>
            {
                ["name"] = ResponseSchemaSpec.EmitToolName,
                ["description"] = "Emit the final answer conforming to the required schema. " +
                                  "Call this exactly once when the task is complete.",
                ["input_schema"] = schema.ResolveSchemaElement()
            });

            if (tools.Count == 0)
            {
                body["tool_choice"] = new Dictionary<string, object?>
                {
                    ["type"] = "tool",
                    ["name"] = ResponseSchemaSpec.EmitToolName
                };
            }
        }

        if (enablePromptCache && toolObjs.Count > 0 && toolObjs[^1] is Dictionary<string, object?> lastTool)
        {
            lastTool["cache_control"] = CacheControlEphemeral();
        }

        if (toolObjs.Count > 0)
        {
            body["tools"] = toolObjs;
        }

        return body;
    }

    private static async Task EnsureSuccessStatusCodeWithBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = response.Content == null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        body = string.IsNullOrWhiteSpace(body) ? "(empty response body)" : body.Trim();
        throw new HttpRequestException(
            $"Response status code {(int)response.StatusCode} ({response.ReasonPhrase}) from Anthropic connector. Body: {body}",
            null,
            response.StatusCode);
    }

    private static LlmCompletionResult ParseNonStreaming(JsonElement root)
    {
        var text = string.Empty;
        var toolCalls = new List<ToolCallRequest>();
        var inputTokens = 0;
        var outputTokens = 0;
        var cacheCreationTokens = 0;
        var cacheReadTokens = 0;

        if (root.TryGetProperty("usage", out var usage))
        {
            ReadTokenUsage(usage, out inputTokens, out outputTokens, out cacheCreationTokens, out cacheReadTokens);
        }

        if (root.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentEl.EnumerateArray())
            {
                if (!block.TryGetProperty("type", out var typeEl))
                    continue;

                var blockType = typeEl.GetString();

                if (blockType == "text" && block.TryGetProperty("text", out var te))
                {
                    text += te.GetString() ?? string.Empty;
                }
                else if (blockType == "tool_use")
                {
                    var callId = block.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
                    var name = block.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
                    var argsJson = "{}";
                    if (block.TryGetProperty("input", out var inputEl) && inputEl.ValueKind != JsonValueKind.Null)
                    {
                        argsJson = inputEl.GetRawText();
                    }

                    toolCalls.Add(new ToolCallRequest
                    {
                        CallId = callId,
                        Name = name,
                        ArgumentsJson = argsJson
                    });
                }
            }
        }

        return new LlmCompletionResult
        {
            TextContent = string.IsNullOrEmpty(text) ? null : text,
            ToolCalls = toolCalls,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheCreationInputTokens = cacheCreationTokens,
            CacheReadInputTokens = cacheReadTokens
        };
    }

    private static void ReadTokenUsage(
        JsonElement usage,
        out int inputTokens,
        out int outputTokens,
        out int cacheCreationInputTokens,
        out int cacheReadInputTokens)
    {
        inputTokens = usage.TryGetProperty("input_tokens", out var it) && it.ValueKind == JsonValueKind.Number
            ? it.GetInt32()
            : 0;
        outputTokens = usage.TryGetProperty("output_tokens", out var ot) && ot.ValueKind == JsonValueKind.Number
            ? ot.GetInt32()
            : 0;
        cacheCreationInputTokens = usage.TryGetProperty("cache_creation_input_tokens", out var cc)
            && cc.ValueKind == JsonValueKind.Number
            ? cc.GetInt32()
            : 0;
        cacheReadInputTokens = usage.TryGetProperty("cache_read_input_tokens", out var cr)
            && cr.ValueKind == JsonValueKind.Number
            ? cr.GetInt32()
            : 0;
    }
}
