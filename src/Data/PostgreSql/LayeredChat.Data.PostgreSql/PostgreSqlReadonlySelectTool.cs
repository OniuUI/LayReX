using System.Text.Json;
using Npgsql;

namespace LayeredChat.Data.PostgreSql;

/// <summary>
/// Tool definition and handler pair for guarded <c>SELECT</c>/<c>WITH</c> reads against PostgreSQL.
/// </summary>
public static class PostgreSqlReadonlySelectTool
{
    public const string ToolName = "postgresql_readonly_select";

    public static ToolDefinition Definition { get; } = new()
    {
        Name = ToolName,
        Description =
            "Run a single read-only PostgreSQL statement (SELECT or WITH). No semicolons except optional trailing. Max rows capped server-side.",
        ParametersSchemaJson =
            """
            {
              "type": "object",
              "properties": {
                "sql": { "type": "string", "description": "Single SELECT or WITH query." },
                "maxRows": { "type": "integer", "minimum": 1, "maximum": 200, "default": 50 }
              },
              "required": ["sql"]
            }
            """
    };

    public static async Task<ToolExecutionResult> ExecuteAsync(
        string argumentsJson,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException ex)
        {
            return new ToolExecutionResult { Success = false, SummaryText = $"Invalid JSON arguments: {ex.Message}" };
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("sql", out var sqlEl))
            {
                return new ToolExecutionResult { Success = false, SummaryText = "Missing sql property." };
            }

            var sql = sqlEl.GetString() ?? string.Empty;
            if (!IsReadOnlySql(sql))
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    SummaryText = "Only single SELECT or WITH statements are allowed."
                };
            }

            var maxRows = 50;
            if (root.TryGetProperty("maxRows", out var mr) && mr.TryGetInt32(out var m))
            {
                maxRows = Math.Clamp(m, 1, 200);
            }

            await using var conn = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var sb = new System.Text.StringBuilder();
            var colCount = reader.FieldCount;
            for (var i = 0; i < colCount; i++)
            {
                if (i > 0)
                {
                    sb.Append(" | ");
                }

                sb.Append(reader.GetName(i));
            }

            sb.AppendLine();
            var n = 0;
            while (n < maxRows && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                for (var c = 0; c < colCount; c++)
                {
                    if (c > 0)
                    {
                        sb.Append(" | ");
                    }

                    var v = reader.IsDBNull(c) ? "" : reader.GetValue(c)?.ToString() ?? "";
                    sb.Append(v.Replace('\n', ' '));
                }

                sb.AppendLine();
                n++;
            }

            return new ToolExecutionResult { Success = true, SummaryText = sb.ToString() };
        }
    }

    private static bool IsReadOnlySql(string sql)
    {
        var trimmed = sql.Trim();
        if (trimmed.Length < 6)
        {
            return false;
        }

        var upper = trimmed.ToUpperInvariant();
        if (!upper.StartsWith("SELECT", StringComparison.Ordinal) && !upper.StartsWith("WITH", StringComparison.Ordinal))
        {
            return false;
        }

        var semi = trimmed.IndexOf(';', StringComparison.Ordinal);
        return semi < 0 || semi == trimmed.Length - 1;
    }
}
