using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// JSON helpers for <see cref="LayerStackManifest"/> (same naming policy as orchestration manifests).
/// </summary>
public static class LayerStackManifestJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string Serialize(LayerStackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, SerializerOptions);
    }

    public static LayerStackManifest Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Layer stack JSON is empty.", nameof(json));
        }

        var manifest = JsonSerializer.Deserialize<LayerStackManifest>(json, SerializerOptions);
        if (manifest is null)
        {
            throw new InvalidOperationException("Layer stack JSON deserialized to null.");
        }

        return manifest;
    }
}
