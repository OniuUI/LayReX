using System.Collections.Concurrent;
using System.Text.Json;

namespace LayeredChat;

/// <summary>
/// How a <see cref="ResponseSchemaSpec"/> is enforced by a connector.
/// </summary>
public enum ResponseSchemaMode
{
    /// <summary>Connector picks the strongest mechanism the adapter profile supports.</summary>
    Auto,

    /// <summary>Provider-native schema-constrained output (OpenAI <c>response_format: json_schema</c>, Gemini <c>responseSchema</c>).</summary>
    NativeJsonSchema,

    /// <summary>A synthetic emit tool carries the schema; the orchestrator folds its call back into text content.</summary>
    ForcedTool,

    /// <summary>Schema is appended as a system instruction; hosts should validate and retry on parse failure.</summary>
    PromptedJson
}

/// <summary>
/// A JSON Schema the model's final answer must conform to. Connectors map this to provider wire formats;
/// the orchestrator folds <see cref="EmitToolName"/> tool calls back into <see cref="LlmCompletionResult.TextContent"/>.
/// </summary>
public sealed class ResponseSchemaSpec
{
    /// <summary>Synthetic tool name used by <see cref="ResponseSchemaMode.ForcedTool"/>.</summary>
    public const string EmitToolName = "emit_result";

    private static readonly ConcurrentDictionary<string, JsonElement> SharedSchemaByJson =
        new(StringComparer.Ordinal);

    /// <summary>Stable schema identifier, e.g. <c>grounded-answer@1</c>. Sanitized before being sent to providers.</summary>
    public required string SchemaName { get; init; }

    /// <summary>Root JSON Schema object as a string.</summary>
    public required string SchemaJson { get; init; }

    public ResponseSchemaMode Mode { get; init; } = ResponseSchemaMode.Auto;

    /// <summary>Maps to OpenAI <c>strict</c> when native mode is used.</summary>
    public bool Strict { get; init; } = true;

    /// <summary>Parses <see cref="SchemaJson"/> once per distinct string (shared cache).</summary>
    public JsonElement ResolveSchemaElement()
    {
        var raw = string.IsNullOrWhiteSpace(SchemaJson) ? "{}" : SchemaJson;
        return SharedSchemaByJson.GetOrAdd(raw, static key =>
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(key) ? "{}" : key);
            return doc.RootElement.Clone();
        });
    }

    /// <summary>
    /// Resolves <see cref="ResponseSchemaMode.Auto"/> using adapter-profile capability flags and the connector's preferred mode.
    /// </summary>
    public ResponseSchemaMode ResolveMode(LlmModelAdapterProfile? profile, ResponseSchemaMode connectorPreferred)
    {
        if (Mode != ResponseSchemaMode.Auto)
        {
            return Mode;
        }

        if (connectorPreferred == ResponseSchemaMode.NativeJsonSchema && (profile?.SupportsNativeJsonSchema ?? true))
        {
            return ResponseSchemaMode.NativeJsonSchema;
        }

        if (profile?.SupportsForcedToolChoice ?? true)
        {
            return ResponseSchemaMode.ForcedTool;
        }

        return ResponseSchemaMode.PromptedJson;
    }

    /// <summary>Provider-safe schema name: <c>[A-Za-z0-9_-]</c>, max 64 chars.</summary>
    public string SanitizedSchemaName()
    {
        var chars = SchemaName
            .Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_')
            .Take(64)
            .ToArray();
        return chars.Length == 0 ? "response" : new string(chars);
    }
}
