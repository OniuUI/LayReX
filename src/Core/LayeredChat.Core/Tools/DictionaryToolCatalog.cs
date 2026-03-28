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
        _tools = tools.ToDictionary(t => t.Name, nameComparer);
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
