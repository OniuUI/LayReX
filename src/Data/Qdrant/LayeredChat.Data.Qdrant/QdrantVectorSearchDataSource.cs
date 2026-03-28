using System.Text;
using System.Text.Json;
using Qdrant.Client;

namespace LayeredChat.Data.Qdrant;

/// <summary>
/// Vector similarity search against Qdrant; query vector comes from manifest parameters or session properties.
/// </summary>
public sealed class QdrantVectorSearchDataSource : IDataSourceProvider
{
    public QdrantVectorSearchDataSource(
        string id,
        QdrantClient client,
        string collectionParameterKey = "qdrant.collection",
        string vectorParameterKey = "qdrant.vectorJson",
        string sessionVectorPropertyKey = "qdrant.queryVectorJson",
        string limitParameterKey = "qdrant.limit",
        int defaultLimit = 12)
    {
        Id = id;
        Client = client;
        CollectionParameterKey = collectionParameterKey;
        VectorParameterKey = vectorParameterKey;
        SessionVectorPropertyKey = sessionVectorPropertyKey;
        LimitParameterKey = limitParameterKey;
        DefaultLimit = defaultLimit;
    }

    public string Id { get; }

    public DataSourceKind Kind => DataSourceKind.VectorSemantic;

    private QdrantClient Client { get; }

    private string CollectionParameterKey { get; }

    private string VectorParameterKey { get; }

    private string SessionVectorPropertyKey { get; }

    private string LimitParameterKey { get; }

    private int DefaultLimit { get; }

    public async Task<ContextSlice> GetSliceAsync(
        OrchestrationSessionContext session,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.TryGetValue(CollectionParameterKey, out var collection) ||
            string.IsNullOrWhiteSpace(collection))
        {
            return new ContextSlice { Label = "Qdrant", Text = $"Missing '{CollectionParameterKey}'." };
        }

        var vectorJson = parameters.TryGetValue(VectorParameterKey, out var vj) && !string.IsNullOrWhiteSpace(vj)
            ? vj
            : session.Properties.TryGetValue(SessionVectorPropertyKey, out var sv)
                ? sv
                : null;

        if (string.IsNullOrWhiteSpace(vectorJson))
        {
            return new ContextSlice
            {
                Label = "Qdrant",
                Text =
                    $"Provide embedding JSON array in '{VectorParameterKey}' or session property '{SessionVectorPropertyKey}'."
            };
        }

        float[] vector;
        try
        {
            vector = JsonSerializer.Deserialize<float[]>(vectorJson) ?? [];
        }
        catch (JsonException ex)
        {
            return new ContextSlice { Label = "Qdrant", Text = $"Bad vector JSON: {ex.Message}" };
        }

        if (vector.Length == 0)
        {
            return new ContextSlice { Label = "Qdrant", Text = "Vector is empty." };
        }

        var limit = DefaultLimit;
        if (parameters.TryGetValue(LimitParameterKey, out var ls) && int.TryParse(ls, out var lp) && lp > 0)
        {
            limit = Math.Min(lp, 100);
        }

        var hits = await Client
            .SearchAsync(
                collection,
                new ReadOnlyMemory<float>(vector),
                limit: (ulong)limit,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("Qdrant nearest payloads (score, payload):");
        foreach (var p in hits)
        {
            sb.Append(p.Score.ToString("F4"));
            sb.Append(" | ");
            sb.AppendLine(p.Payload?.ToString() ?? "{}");
        }

        return new ContextSlice { Label = "Qdrant", Text = sb.ToString() };
    }
}
