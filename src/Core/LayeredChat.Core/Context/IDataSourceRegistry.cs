namespace LayeredChat;

/// <summary>
/// Resolves <see cref="IDataSourceProvider"/> instances referenced from a manifest.
/// </summary>
public interface IDataSourceRegistry
{
    bool TryGet(string id, out IDataSourceProvider? provider);
}
