using System.Text.Json;

namespace LayeredChat.Tests;

public sealed class ToolDefinitionResolveParametersTests
{
    [Fact]
    public void ResolveParametersElement_reuses_cache_for_same_schema_json_string()
    {
        var schema = """{"type":"object","properties":{"x":{"type":"string"}}}""";
        var a = new ToolDefinition { Name = "a", ParametersSchemaJson = schema };
        var b = new ToolDefinition { Name = "b", ParametersSchemaJson = schema };
        var ja = JsonSerializer.Serialize(a.ResolveParametersElement());
        var jb = JsonSerializer.Serialize(b.ResolveParametersElement());
        Assert.Equal(ja, jb);
    }

    [Fact]
    public void DictionaryToolCatalog_embeds_parameters_schema_clone()
    {
        var raw = """{"type":"object"}""";
        var catalog = new DictionaryToolCatalog([new ToolDefinition { Name = "t", ParametersSchemaJson = raw }]);
        Assert.True(catalog.TryGet("t", out var def));
        Assert.NotNull(def);
        Assert.True(def!.ParametersSchema.HasValue);
        using var expected = JsonDocument.Parse(raw);
        Assert.Equal(
            JsonSerializer.Serialize(expected.RootElement),
            JsonSerializer.Serialize(def.ParametersSchema.Value));
    }
}
