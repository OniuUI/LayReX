namespace LayeredChat.Tests;

public sealed class ManifestJsonTests
{
    [Fact]
    public void RoundTrips_manifest_fields()
    {
        var original = new OrchestrationProfileManifest
        {
            SchemaVersion = 1,
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            Description = "Test profile",
            AllowedToolNames = new[] { "alpha", "beta" },
            DataSourceIdsInOrder = new[] { "rag", "sql" },
            OutputCapabilities = OrchestrationOutputCapabilities.TextReply | OrchestrationOutputCapabilities.ToolCalls,
            MaxToolIterations = 4,
            DefaultTemperature = 0.7,
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rag.topK"] = "5"
            }
        };

        var json = OrchestrationProfileManifestJson.Serialize(original);
        var restored = OrchestrationProfileManifestJson.Deserialize(json);

        Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.OrchestrationId, restored.OrchestrationId);
        Assert.Equal(original.SemanticVersion, restored.SemanticVersion);
        Assert.Equal(original.DisplayName, restored.DisplayName);
        Assert.Equal(original.Description, restored.Description);
        Assert.Equal(original.AllowedToolNames, restored.AllowedToolNames);
        Assert.Equal(original.DataSourceIdsInOrder, restored.DataSourceIdsInOrder);
        Assert.Equal(original.OutputCapabilities, restored.OutputCapabilities);
        Assert.Equal(original.MaxToolIterations, restored.MaxToolIterations);
        Assert.Equal(original.DefaultTemperature, restored.DefaultTemperature);
        Assert.Equal("5", restored.Parameters["rag.topK"]);
        Assert.Null(restored.LayerStack);
    }
}
