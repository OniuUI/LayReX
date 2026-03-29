namespace LayeredChat.Tests;

public sealed class LayerCompositionTests
{
    [Fact]
    public void Compose_empty_layers_copies_baseline_and_clears_layer_stack()
    {
        var baseline = new OrchestrationProfileManifest
        {
            SchemaVersion = 1,
            OrchestrationId = "app.demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            AllowedToolNames = new[] { "a" },
            MaxToolIterations = 3,
            LayerStack = new LayerStackDeclaration
            {
                SchemaVersion = 1,
                Entries = new[] { new LayerReferenceEntry { LayerId = "x", Version = "1.0.0" } }
            }
        };

        var svc = new LayerCompositionService();
        var result = svc.Compose(baseline, []);

        Assert.Null(result.EffectiveManifest.LayerStack);
        Assert.Equal(baseline.OrchestrationId, result.EffectiveManifest.OrchestrationId);
        Assert.Equal(baseline.AllowedToolNames, result.EffectiveManifest.AllowedToolNames);
        Assert.Equal(baseline.MaxToolIterations, result.EffectiveManifest.MaxToolIterations);
        Assert.Empty(result.InstructionFragments);
    }

    [Fact]
    public void Compose_unions_tools_and_data_sources_preserves_order()
    {
        var baseline = new OrchestrationProfileManifest
        {
            OrchestrationId = "o",
            SemanticVersion = "1.0.0",
            AllowedToolNames = new[] { "first" },
            DataSourceIdsInOrder = new[] { "ds1" },
            MaxToolIterations = 2,
            DefaultTemperature = 0.1
        };

        var layers = new[]
        {
            new LayerContribution
            {
                LayerId = "L1",
                SemanticVersion = "1.0.0",
                AllowedToolNames = new[] { "second", "first" },
                DataSourceIdsInOrder = new[] { "ds2" },
                MaxToolIterations = 5,
                DefaultTemperature = 0.5,
                InstructionFragment = "  hello  "
            },
            new LayerContribution
            {
                LayerId = "L2",
                SemanticVersion = "1.0.0",
                DefaultTemperature = 0.9,
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["k"] = "v" }
            }
        };

        var result = LayerComposition.Compose(baseline, layers);

        Assert.Equal(new[] { "first", "second" }, result.EffectiveManifest.AllowedToolNames);
        Assert.Equal(new[] { "ds1", "ds2" }, result.EffectiveManifest.DataSourceIdsInOrder);
        Assert.Equal(5, result.EffectiveManifest.MaxToolIterations);
        Assert.Equal(0.9, result.EffectiveManifest.DefaultTemperature);
        Assert.Equal("v", result.EffectiveManifest.Parameters["k"]);
        Assert.Single(result.InstructionFragments);
        Assert.Equal("hello", result.InstructionFragments[0]);
        Assert.Equal("hello", result.JoinInstructionFragments("\n\n"));
    }

    [Fact]
    public void ManifestJson_round_trips_layer_stack()
    {
        var original = new OrchestrationProfileManifest
        {
            OrchestrationId = "x",
            SemanticVersion = "1.0.0",
            DisplayName = "X",
            LayerStack = new LayerStackDeclaration
            {
                SchemaVersion = 1,
                Entries =
                [
                    new LayerReferenceEntry { LayerId = "base", Version = "1.0.0" },
                    new LayerReferenceEntry { LayerId = "addon", Version = "2.1.0" }
                ]
            }
        };

        var json = OrchestrationProfileManifestJson.Serialize(original);
        var restored = OrchestrationProfileManifestJson.Deserialize(json);

        Assert.NotNull(restored.LayerStack);
        Assert.Equal(2, restored.LayerStack!.Entries.Count);
        Assert.Equal("base", restored.LayerStack.Entries[0].LayerId);
        Assert.Equal("1.0.0", restored.LayerStack.Entries[0].Version);
    }
}
