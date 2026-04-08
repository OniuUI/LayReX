using System.Collections.Concurrent;
using System.Text.Json;

namespace LayeredChat;

/// <summary>
/// JSON-schema-backed tool exposed to the model. The host maps these to <see cref="IToolExecutor"/> implementations.
/// </summary>
public sealed class ToolDefinition
{
    private static readonly ConcurrentDictionary<string, JsonElement> SharedParameterSchemaByJson =
        new(StringComparer.Ordinal);

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Root JSON Schema object for parameters (provider-specific wrapping may apply in the connector).
    /// </summary>
    public string ParametersSchemaJson { get; init; } = "{}";

    /// <summary>
    /// When set, connectors use this value as the parsed parameters schema and skip parsing <see cref="ParametersSchemaJson"/>.
    /// </summary>
    public JsonElement? ParametersSchema { get; init; }

    /// <summary>
    /// Returns <see cref="ParametersSchema"/> when set; otherwise parses <see cref="ParametersSchemaJson"/> once per distinct string (shared cache).
    /// </summary>
    public JsonElement ResolveParametersElement()
    {
        if (ParametersSchema is { } embedded)
        {
            return embedded;
        }

        var raw = string.IsNullOrWhiteSpace(ParametersSchemaJson) ? "{}" : ParametersSchemaJson;
        return SharedParameterSchemaByJson.GetOrAdd(raw, static key =>
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(key) ? "{}" : key);
            return doc.RootElement.Clone();
        });
    }
}
