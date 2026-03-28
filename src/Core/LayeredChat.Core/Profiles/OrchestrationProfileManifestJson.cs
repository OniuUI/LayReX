using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// JSON helpers for persisting and loading <see cref="OrchestrationProfileManifest"/> documents.
/// </summary>
public static class OrchestrationProfileManifestJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string Serialize(OrchestrationProfileManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, SerializerOptions);
    }

    public static OrchestrationProfileManifest Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Manifest JSON is empty.", nameof(json));
        }

        var manifest = JsonSerializer.Deserialize<OrchestrationProfileManifest>(json, SerializerOptions);
        if (manifest is null)
        {
            throw new InvalidOperationException("Manifest JSON deserialized to null.");
        }

        return manifest;
    }
}
