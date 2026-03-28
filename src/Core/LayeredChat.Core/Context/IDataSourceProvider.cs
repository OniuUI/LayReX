namespace LayeredChat;

/// <summary>
/// Supplies one contextual slice (RAG, SQL summary, config, etc.). Hosts register providers by id referenced from <see cref="OrchestrationProfileManifest"/>.
/// </summary>
public interface IDataSourceProvider
{
    string Id { get; }

    DataSourceKind Kind { get; }

    Task<ContextSlice> GetSliceAsync(
        OrchestrationSessionContext session,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);
}
