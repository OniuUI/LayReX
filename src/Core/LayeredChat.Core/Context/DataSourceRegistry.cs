namespace LayeredChat;

/// <summary>
/// In-memory data source registry. Hosts may compose multiple providers into one registry facade.
/// </summary>
public sealed class DataSourceRegistry : IDataSourceRegistry
{
    private readonly Dictionary<string, IDataSourceProvider> _providers;

    public DataSourceRegistry(IEnumerable<IDataSourceProvider> providers, StringComparer? idComparer = null)
    {
        ArgumentNullException.ThrowIfNull(providers);
        idComparer ??= StringComparer.OrdinalIgnoreCase;
        _providers = providers.ToDictionary(p => p.Id, idComparer);
    }

    public bool TryGet(string id, out IDataSourceProvider? provider)
    {
        return _providers.TryGetValue(id, out provider);
    }
}
