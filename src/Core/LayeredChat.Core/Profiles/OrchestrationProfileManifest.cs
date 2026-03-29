using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// Serializable orchestration profile: tool allow-list, ordered data sources, loop policy, and declared output surface.
/// Hosts store drafts and published versions as JSON (file, blob, or database text).
/// </summary>
public sealed class OrchestrationProfileManifest
{
    public int SchemaVersion { get; init; } = 1;

    public string OrchestrationId { get; init; } = string.Empty;

    public string? SemanticVersion { get; init; }

    /// <summary>Optional explicit profile SemVer for host configuration; registry keys still use <see cref="SemanticVersion"/>.</summary>
    public string? ProfileVersion { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<string> AllowedToolNames { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<string> DataSourceIdsInOrder { get; init; } = [];

    public OrchestrationOutputCapabilities OutputCapabilities { get; init; } =
        OrchestrationOutputCapabilities.TextReply | OrchestrationOutputCapabilities.ToolCalls;

    public int MaxToolIterations { get; init; } = 8;

    public double DefaultTemperature { get; init; } = 0.2;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? ExternalForwardUri { get; init; }

    public int ExternalForwardTimeoutSeconds { get; init; } = 120;

    public string? LlmAdapterProfileId { get; init; }

    public string? PreferredConnectorKind { get; init; }

    /// <summary>Optional ordered layer references. Hosts resolve each to a <see cref="LayerContribution"/> and call <see cref="ILayerCompositionService.Compose"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public LayerStackDeclaration? LayerStack { get; init; }
}
