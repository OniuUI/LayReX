using System.Collections.Concurrent;
using System.Text.Json;
using LayeredChat;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace LayeredChat.Integrations.Mcp;

/// <summary>
/// Connects an MCP client session and exposes discovered tools as <see cref="IToolCatalog"/> and <see cref="IToolExecutor"/> for <see cref="LayeredChatOrchestrator"/>.
/// </summary>
public sealed class McpToolSession : IAsyncDisposable
{
    private readonly McpClient _client;
    private readonly string _namePrefix;
    private readonly ConcurrentDictionary<string, string> _mcpNameByLayeredName = new(StringComparer.Ordinal);

    private McpToolSession(McpClient client, string namePrefix)
    {
        _client = client;
        _namePrefix = namePrefix;
    }

    /// <summary>
    /// Creates a session over stdio (local process). Tool names exposed to the LLM are optionally prefixed to avoid collisions across multiple MCP servers.
    /// </summary>
    public static async Task<McpToolSession> ConnectStdioAsync(
        StdioClientTransportOptions transportOptions,
        McpSessionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transportOptions);
        var transport = new StdioClientTransport(transportOptions);
        return await ConnectAsync(transport, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a session from any MCP client transport (stdio, streamable HTTP, etc.) supported by the SDK.
    /// </summary>
    public static async Task<McpToolSession> ConnectAsync(
        IClientTransport clientTransport,
        McpSessionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientTransport);
        options ??= new McpSessionOptions();
        var client = await McpClient.CreateAsync(clientTransport, cancellationToken: cancellationToken).ConfigureAwait(false);
        var session = new McpToolSession(client, options.ToolNamePrefix);
        await session.RefreshToolsAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <summary>
    /// Underlying MCP client for prompts, resources, or advanced scenarios.
    /// </summary>
    public McpClient Client => _client;

    /// <summary>
    /// Catalog backed by the last <see cref="RefreshToolsAsync"/> snapshot.
    /// </summary>
    public IToolCatalog Catalog { get; private set; } = new DictionaryToolCatalog([]);

    /// <summary>
    /// Executor that forwards tool calls to MCP for names known to this session.
    /// </summary>
    public IToolExecutor Executor { get; private set; } = new McpOnlyToolExecutor((_, _, _, _) =>
        throw new InvalidOperationException("Call RefreshToolsAsync or ConnectStdioAsync before using the MCP executor."));

    private static string ToSchemaJson(JsonElement? schema)
    {
        if (schema is null || schema.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return "{}";
        }

        return schema.Value.GetRawText();
    }

    /// <summary>
    /// Reloads tools from the server and rebuilds <see cref="Catalog"/> and <see cref="Executor"/>.
    /// </summary>
    public async Task RefreshToolsAsync(CancellationToken cancellationToken = default)
    {
        var listed = await _client.ListToolsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _mcpNameByLayeredName.Clear();
        var map = new Dictionary<string, ToolDefinition>(StringComparer.Ordinal);
        foreach (var t in listed)
        {
            var layered = string.IsNullOrEmpty(_namePrefix) ? t.Name : $"{_namePrefix}{t.Name}";
            _mcpNameByLayeredName[layered] = t.Name;
            map[layered] = new ToolDefinition
            {
                Name = layered,
                Description = t.Description ?? string.Empty,
                ParametersSchemaJson = ToSchemaJson(t.JsonSchema)
            };
        }

        Catalog = new DictionaryToolCatalog(map.Values);
        Executor = new McpOnlyToolExecutor(async (layeredName, argumentsJson, _, ct) =>
        {
            if (!_mcpNameByLayeredName.TryGetValue(layeredName, out var mcpName))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    SummaryText = $"Unknown MCP tool mapping for '{layeredName}'."
                };
            }

            var args = ParseArguments(argumentsJson);
            var result = await _client.CallToolAsync(mcpName, args, cancellationToken: ct).ConfigureAwait(false);
            return ToToolResult(result);
        });
    }

    private static Dictionary<string, object?> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object?>();
        }

        using var doc = JsonDocument.Parse(argumentsJson);
        if (doc.RootElement.ValueKind is not JsonValueKind.Object)
        {
            return new Dictionary<string, object?>();
        }

        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }

        return dict;
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            _ => el.GetRawText()
        };
    }

    private static ToolExecutionResult ToToolResult(CallToolResult result)
    {
        var parts = new List<string>();
        foreach (var block in result.Content)
        {
            if (block is TextContentBlock text)
            {
                parts.Add(text.Text);
            }
            else
            {
                parts.Add(block.ToString() ?? string.Empty);
            }
        }

        var summary = parts.Count > 0 ? string.Join("\n", parts) : "(no content)";
        return new ToolExecutionResult
        {
            Success = result.IsError is not true,
            SummaryText = summary
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Options for <see cref="McpToolSession"/>.
/// </summary>
public sealed class McpSessionOptions
{
    /// <summary>
    /// Prepended to each MCP tool name for the LLM (e.g. <c>weather_</c>). Use when wiring several MCP servers.
    /// </summary>
    public string ToolNamePrefix { get; init; } = string.Empty;
}

internal sealed class McpOnlyToolExecutor : IToolExecutor
{
    private readonly Func<string, string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>> _invoke;

    public McpOnlyToolExecutor(Func<string, string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>> invoke)
    {
        _invoke = invoke;
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default)
    {
        return _invoke(toolName, argumentsJson, session, cancellationToken);
    }
}
