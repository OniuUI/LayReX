using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// Resolved contributions from one layer package, merged in stack order into an effective manifest.
/// </summary>
public sealed class LayerContribution
{
    public int SchemaVersion { get; init; } = 1;

    public string LayerId { get; init; } = string.Empty;

    public string SemanticVersion { get; init; } = string.Empty;

    public string? InstructionFragment { get; init; }

    /// <summary>Markdown or text files relative to the layer version directory; hosts concatenate contents after <see cref="InstructionFragment"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<string> InstructionMarkdownFiles { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<string> AllowedToolNames { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<string> DataSourceIdsInOrder { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public int? MaxToolIterations { get; init; }

    public double? DefaultTemperature { get; init; }

    public OrchestrationOutputCapabilities? OutputCapabilities { get; init; }
}
