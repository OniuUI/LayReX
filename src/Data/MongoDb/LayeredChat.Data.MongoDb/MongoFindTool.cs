using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LayeredChat.Data.MongoDb;

/// <summary>
/// Tool for bounded MongoDB <c>find</c> operations described by JSON filter text.
/// </summary>
public static class MongoFindTool
{
    public const string ToolName = "mongodb_find_json";

    public static ToolDefinition Definition { get; } = new()
    {
        Name = ToolName,
        Description = "Find MongoDB documents in a collection using an extended JSON filter object.",
        ParametersSchemaJson =
            """
            {
              "type": "object",
              "properties": {
                "collection": { "type": "string" },
                "filterJson": { "type": "string", "default": "{}" },
                "limit": { "type": "integer", "minimum": 1, "maximum": 200, "default": 30 }
              },
              "required": ["collection"]
            }
            """
    };

    public static async Task<ToolExecutionResult> ExecuteAsync(
        string argumentsJson,
        IMongoDatabase database,
        CancellationToken cancellationToken = default)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException ex)
        {
            return new ToolExecutionResult { Success = false, SummaryText = $"Invalid JSON: {ex.Message}" };
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("collection", out var cEl))
            {
                return new ToolExecutionResult { Success = false, SummaryText = "Missing collection." };
            }

            var coll = cEl.GetString() ?? string.Empty;
            var filterText = root.TryGetProperty("filterJson", out var f) ? f.GetString() ?? "{}" : "{}";
            var limit = 30;
            if (root.TryGetProperty("limit", out var l) && l.TryGetInt32(out var li))
            {
                limit = Math.Clamp(li, 1, 200);
            }

            BsonDocument filter;
            try
            {
                filter = BsonDocument.Parse(filterText);
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { Success = false, SummaryText = $"Bad filter: {ex.Message}" };
            }

            var collection = database.GetCollection<BsonDocument>(coll);
            var cursor = await collection
                .Find(filter)
                .Limit(limit)
                .ToCursorAsync(cancellationToken)
                .ConfigureAwait(false);

            var sb = new System.Text.StringBuilder();
            while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var bson in cursor.Current)
                {
                    sb.AppendLine(bson.ToJson());
                }
            }

            return new ToolExecutionResult { Success = true, SummaryText = sb.ToString() };
        }
    }
}
