namespace LayeredChat;

/// <summary>
/// Tries multiple registries in order so hosts can compose built-in, tenant-scoped, and manifest-specific providers.
/// </summary>
public sealed class CompositeDataSourceRegistry : IDataSourceRegistry
{
    private readonly IReadOnlyList<IDataSourceRegistry> _registries;

    public CompositeDataSourceRegistry(IEnumerable<IDataSourceRegistry> registries)
    {
        _registries = registries.ToList();
    }

    public bool TryGet(string id, out IDataSourceProvider? provider)
    {
        foreach (var registry in _registries)
        {
            if (registry.TryGet(id, out provider) && provider is not null)
            {
                return true;
            }
        }

        provider = null;
        return false;
    }
}
