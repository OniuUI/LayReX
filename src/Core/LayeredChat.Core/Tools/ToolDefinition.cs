namespace LayeredChat;

/// <summary>
/// JSON-schema-backed tool exposed to the model. The host maps these to <see cref="IToolExecutor"/> implementations.
/// </summary>
public sealed class ToolDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Root JSON Schema object for parameters (provider-specific wrapping may apply in the connector).
    /// </summary>
    public string ParametersSchemaJson { get; init; } = "{}";
}
