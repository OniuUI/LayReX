using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// Optional embedded stack on <see cref="OrchestrationProfileManifest"/>. When null or empty entries, legacy single-manifest behavior applies.
/// </summary>
public sealed class LayerStackDeclaration
{
    public int SchemaVersion { get; init; } = 1;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<LayerReferenceEntry> Entries { get; init; } = [];
}
