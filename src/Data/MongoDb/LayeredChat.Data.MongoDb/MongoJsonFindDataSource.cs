using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LayeredChat.Data.MongoDb;

/// <summary>
/// Runs a <c>find</c> with a JSON filter from manifest parameters and serializes matching documents as JSON lines.
/// </summary>
public sealed class MongoJsonFindDataSource : IDataSourceProvider
{
    public MongoJsonFindDataSource(
        string id,
        IMongoDatabase database,
        string collectionParameterKey = "mongo.collection",
        string filterParameterKey = "mongo.filterJson",
        string limitParameterKey = "mongo.limit",
        int defaultLimit = 40)
    {
        Id = id;
        Database = database;
        CollectionParameterKey = collectionParameterKey;
        FilterParameterKey = filterParameterKey;
        LimitParameterKey = limitParameterKey;
        DefaultLimit = defaultLimit;
    }

    public string Id { get; }

    public DataSourceKind Kind => DataSourceKind.SqlTabular;

    private IMongoDatabase Database { get; }

    private string CollectionParameterKey { get; }

    private string FilterParameterKey { get; }

    private string LimitParameterKey { get; }

    private int DefaultLimit { get; }

    public async Task<ContextSlice> GetSliceAsync(
        OrchestrationSessionContext session,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.TryGetValue(CollectionParameterKey, out var collName) || string.IsNullOrWhiteSpace(collName))
        {
            return new ContextSlice
            {
                Label = "MongoDB",
                Text = $"Missing '{CollectionParameterKey}'."
            };
        }

        var filterJson = parameters.TryGetValue(FilterParameterKey, out var fj) ? fj : "{}";
        BsonDocument filter;
        try
        {
            filter = BsonDocument.Parse(filterJson);
        }
        catch (Exception ex)
        {
            return new ContextSlice
            {
                Label = "MongoDB",
                Text = $"Invalid filter JSON: {ex.Message}"
            };
        }

        var limit = DefaultLimit;
        if (parameters.TryGetValue(LimitParameterKey, out var ls) && int.TryParse(ls, out var lp) && lp > 0)
        {
            limit = Math.Min(lp, 200);
        }

        var collection = Database.GetCollection<BsonDocument>(collName);
        var cursor = await collection
            .Find(filter)
            .Limit(limit)
            .ToCursorAsync(cancellationToken)
            .ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("MongoDB documents (JSON lines):");
        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var doc in cursor.Current)
            {
                sb.AppendLine(doc.ToJson());
            }
        }

        return new ContextSlice { Label = "MongoDB", Text = sb.ToString() };
    }
}
