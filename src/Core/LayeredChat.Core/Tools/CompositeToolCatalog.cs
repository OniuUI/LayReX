namespace LayeredChat;

/// <summary>
/// Tries inner <see cref="IToolCatalog"/> instances in order; the first match wins for <see cref="TryGet"/>.
/// </summary>
public sealed class CompositeToolCatalog : IToolCatalog
{
    private readonly IReadOnlyList<IToolCatalog> _catalogsInOrder;

    public CompositeToolCatalog(IEnumerable<IToolCatalog> catalogsInOrder)
    {
        ArgumentNullException.ThrowIfNull(catalogsInOrder);
        _catalogsInOrder = catalogsInOrder.ToList();
        if (_catalogsInOrder.Count == 0)
        {
            throw new ArgumentException("At least one catalog is required.", nameof(catalogsInOrder));
        }
    }

    public bool TryGet(string name, out ToolDefinition? definition)
    {
        foreach (var catalog in _catalogsInOrder)
        {
            if (catalog.TryGet(name, out definition) && definition is not null)
            {
                return true;
            }
        }

        definition = null;
        return false;
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

            if (TryGet(name, out var def) && def is not null)
            {
                list.Add(def);
            }
        }

        return list;
    }
}
