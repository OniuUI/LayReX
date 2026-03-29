using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// Standalone document listing ordered layer references (e.g. <c>stack.json</c> in a layer bundle).
/// </summary>
public sealed class LayerStackManifest
{
    public int SchemaVersion { get; init; } = 1;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<LayerReferenceEntry> Layers { get; init; } = [];
}
