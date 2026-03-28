using System.Runtime.CompilerServices;
using System.Text.Json;
using Me = Microsoft.Extensions.AI;

namespace LayeredChat.Connectors.ExtensionsAi;

/// <summary>
/// Bridges <see cref="Me.IChatClient"/> to <see cref="ILlmChatConnector"/> and <see cref="IStreamingLlmChatConnector"/>.
/// Tool schemas are passed as declaration-only <see cref="Me.AIFunctionDeclaration"/> entries suitable for any MEAI-backed provider.
/// </summary>
public sealed class ExtensionsAiChatConnector : IStreamingLlmChatConnector
{
    private readonly Me.IChatClient _client;

    public ExtensionsAiChatConnector(Me.IChatClient client, string connectorKind = "ExtensionsAi")
    {
        _client = client;
        ConnectorKind = connectorKind;
    }

    public string ConnectorKind { get; }

    public async Task<LlmCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        var meMessages = MapMessages(messages);
        var chatOptions = BuildChatOptions(tools, options);
        var response = await _client
            .GetResponseAsync(meMessages, chatOptions, cancellationToken)
            .ConfigureAwait(false);

        return MapResponse(response);
    }

    public async IAsyncEnumerable<LlmStreamFrame> CompleteStreamingAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition> tools,
        LlmRequestOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var meMessages = MapMessages(messages);
        var chatOptions = BuildChatOptions(tools, options);
        await foreach (var update in _client
                           .GetStreamingResponseAsync(meMessages, chatOptions, cancellationToken)
                           .ConfigureAwait(false))
        {
            foreach (var frame in MapUpdate(update))
            {
                yield return frame;
            }
        }

        yield return new LlmStreamFrame { Kind = LlmStreamFrameKind.Completed };
    }

    private static List<Me.ChatMessage> MapMessages(IReadOnlyList<ChatMessage> messages)
    {
        var list = new List<Me.ChatMessage>();
        foreach (var m in messages)
        {
            switch (m.Role)
            {
                case ChatRole.System:
                    list.Add(new Me.ChatMessage(Me.ChatRole.System, m.Content));
                    break;
                case ChatRole.User:
                    list.Add(new Me.ChatMessage(Me.ChatRole.User, m.Content));
                    break;
                case ChatRole.Assistant:
                {
                    if (m.ToolCalls is { Count: > 0 } tc)
                    {
                        var parts = new List<Me.AIContent>();
                        if (!string.IsNullOrEmpty(m.Content))
                        {
                            parts.Add(new Me.TextContent(m.Content));
                        }

                        foreach (var call in tc)
                        {
                            var argsDict = ParseArgumentsDictionary(call.ArgumentsJson);
                            parts.Add(new Me.FunctionCallContent(call.CallId, call.Name, argsDict));
                        }

                        list.Add(new Me.ChatMessage(Me.ChatRole.Assistant, parts));
                    }
                    else
                    {
                        list.Add(new Me.ChatMessage(Me.ChatRole.Assistant, m.Content));
                    }

                    break;
                }
                case ChatRole.Tool:
                    list.Add(new Me.ChatMessage(Me.ChatRole.Tool,
                        [new Me.FunctionResultContent(m.ToolCallId ?? string.Empty, m.Content)]));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(messages), m.Role, "Unsupported chat role.");
            }
        }

        return list;
    }

    private static Dictionary<string, object?>? ParseArgumentsDictionary(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?> { ["raw"] = argumentsJson };
        }
    }

    private static Me.ChatOptions BuildChatOptions(IReadOnlyList<ToolDefinition> tools, LlmRequestOptions options)
    {
        var chatOptions = new Me.ChatOptions
        {
            Temperature = (float?)options.Temperature,
            MaxOutputTokens = options.MaxOutputTokens
        };

        if (!string.IsNullOrEmpty(options.ModelNameOverride))
        {
            chatOptions.ModelId = options.ModelNameOverride;
        }

        if (tools.Count > 0)
        {
            var meTools = new List<Me.AITool>();
            foreach (var t in tools)
            {
                var schema = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(t.ParametersSchemaJson)
                    ? "{}"
                    : t.ParametersSchemaJson);
                var decl = Me.AIFunctionFactory.CreateDeclaration(t.Name, t.Description, schema, null);
                meTools.Add(decl);
            }

            chatOptions.Tools = meTools;
        }

        return chatOptions;
    }

    private static LlmCompletionResult MapResponse(Me.ChatResponse response)
    {
        var text = response.Text;
        var toolCalls = new List<ToolCallRequest>();
        foreach (var msg in response.Messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is Me.FunctionCallContent fc)
                {
                    var args = fc.Arguments is null ? "{}" : JsonSerializer.Serialize(fc.Arguments);
                    toolCalls.Add(new ToolCallRequest
                    {
                        CallId = fc.CallId,
                        Name = fc.Name,
                        ArgumentsJson = args
                    });
                }
            }
        }

        var usage = response.Usage;
        return new LlmCompletionResult
        {
            TextContent = string.IsNullOrEmpty(text) ? null : text,
            ToolCalls = toolCalls,
            InputTokens = usage is null ? 0 : (int)Math.Min(int.MaxValue, usage.InputTokenCount ?? 0),
            OutputTokens = usage is null ? 0 : (int)Math.Min(int.MaxValue, usage.OutputTokenCount ?? 0)
        };
    }

    private static IEnumerable<LlmStreamFrame> MapUpdate(Me.ChatResponseUpdate update)
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            yield return new LlmStreamFrame
            {
                Kind = LlmStreamFrameKind.TextDelta,
                TextDelta = update.Text
            };
        }

        foreach (var content in update.Contents)
        {
            if (content is Me.FunctionCallContent fc)
            {
                yield return new LlmStreamFrame
                {
                    Kind = LlmStreamFrameKind.ToolCallMeta,
                    ToolCallId = fc.CallId,
                    ToolName = fc.Name
                };

                var args = fc.Arguments is null ? string.Empty : JsonSerializer.Serialize(fc.Arguments);
                if (!string.IsNullOrEmpty(args))
                {
                    yield return new LlmStreamFrame
                    {
                        Kind = LlmStreamFrameKind.ToolArgumentsDelta,
                        ToolArgumentsDelta = args
                    };
                }
            }
        }

        if (update.Contents is { Count: > 0 })
        {
            foreach (var c in update.Contents)
            {
                if (c is Me.UsageContent u)
                {
                    yield return new LlmStreamFrame
                    {
                        Kind = LlmStreamFrameKind.Usage,
                        InputTokens = u.Details.InputTokenCount is { } i ? (int)Math.Min(int.MaxValue, i) : null,
                        OutputTokens = u.Details.OutputTokenCount is { } o ? (int)Math.Min(int.MaxValue, o) : null
                    };
                }
            }
        }
    }
}
