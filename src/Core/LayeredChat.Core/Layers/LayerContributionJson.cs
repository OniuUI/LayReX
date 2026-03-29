using System.Text.Json;
using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// JSON helpers for <c>layer.json</c> inside a layer package.
/// </summary>
public static class LayerContributionJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string Serialize(LayerContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        return JsonSerializer.Serialize(contribution, SerializerOptions);
    }

    public static LayerContribution Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Layer JSON is empty.", nameof(json));
        }

        var contribution = JsonSerializer.Deserialize<LayerContribution>(json, SerializerOptions);
        if (contribution is null)
        {
            throw new InvalidOperationException("Layer JSON deserialized to null.");
        }

        return contribution;
    }
}
