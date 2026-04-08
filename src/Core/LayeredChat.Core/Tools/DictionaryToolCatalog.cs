using System.Text.Json;

namespace LayeredChat;

/// <summary>
/// Simple <see cref="IToolCatalog"/> backed by a name-to-definition map.
/// </summary>
public sealed class DictionaryToolCatalog : IToolCatalog
{
    private readonly Dictionary<string, ToolDefinition> _tools;

    public DictionaryToolCatalog(IEnumerable<ToolDefinition> tools, StringComparer? nameComparer = null)
    {
        ArgumentNullException.ThrowIfNull(tools);
        nameComparer ??= StringComparer.Ordinal;
        _tools = tools.Select(WithEmbeddedParametersSchema).ToDictionary(t => t.Name, nameComparer);
    }

    private static ToolDefinition WithEmbeddedParametersSchema(ToolDefinition t)
    {
        if (t.ParametersSchema.HasValue)
        {
            return t;
        }

        var json = string.IsNullOrWhiteSpace(t.ParametersSchemaJson) ? "{}" : t.ParametersSchemaJson.Trim();
        using var doc = JsonDocument.Parse(json);
        return new ToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            ParametersSchemaJson = t.ParametersSchemaJson,
            ParametersSchema = doc.RootElement.Clone()
        };
    }

    public bool TryGet(string name, out ToolDefinition? definition)
    {
        return _tools.TryGetValue(name, out definition);
    }

    public IReadOnlyList<ToolDefinition> ResolveAllowed(IReadOnlyList<string> allowedNames)
    {
        ArgumentNullException.ThrowIfNull(allowedNames);
        var list = new List<ToolDefinition>();
        foreach (var name in allowedNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (_tools.TryGetValue(name, out var def) && def is not null)
            {
                list.Add(def);
            }
        }

        return list;
    }
}
