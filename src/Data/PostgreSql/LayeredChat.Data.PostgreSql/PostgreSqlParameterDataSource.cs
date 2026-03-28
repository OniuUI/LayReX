using System.Text;
using Npgsql;

namespace LayeredChat.Data.PostgreSql;

/// <summary>
/// Runs a SQL statement taken from manifest parameters (for example <c>pgsql.sql</c>) and formats rows as a compact Markdown table for the model.
/// </summary>
public sealed class PostgreSqlParameterDataSource : IDataSourceProvider
{
    public PostgreSqlParameterDataSource(
        string id,
        NpgsqlDataSource dataSource,
        string sqlParameterKey = "pgsql.sql",
        string maxRowsParameterKey = "pgsql.maxRows",
        int defaultMaxRows = 80)
    {
        Id = id;
        DataSource = dataSource;
        SqlParameterKey = sqlParameterKey;
        MaxRowsParameterKey = maxRowsParameterKey;
        DefaultMaxRows = defaultMaxRows;
    }

    public string Id { get; }

    public DataSourceKind Kind => DataSourceKind.SqlTabular;

    private NpgsqlDataSource DataSource { get; }

    private string SqlParameterKey { get; }

    private string MaxRowsParameterKey { get; }

    private int DefaultMaxRows { get; }

    public async Task<ContextSlice> GetSliceAsync(
        OrchestrationSessionContext session,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.TryGetValue(SqlParameterKey, out var sql) || string.IsNullOrWhiteSpace(sql))
        {
            return new ContextSlice
            {
                Label = "PostgreSQL",
                Text = $"Missing parameter '{SqlParameterKey}' with SQL text."
            };
        }

        var maxRows = DefaultMaxRows;
        if (parameters.TryGetValue(MaxRowsParameterKey, out var mr) && int.TryParse(mr, out var parsed) && parsed > 0)
        {
            maxRows = Math.Min(parsed, 500);
        }

        await using var conn = await DataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine("PostgreSQL result (Markdown table):");
        var colCount = reader.FieldCount;
        if (colCount == 0)
        {
            return new ContextSlice { Label = "PostgreSQL", Text = sb.ToString() };
        }

        for (var i = 0; i < colCount; i++)
        {
            if (i > 0)
            {
                sb.Append(" | ");
            }

            sb.Append(reader.GetName(i));
        }

        sb.AppendLine();
        sb.AppendLine(new string('-', Math.Min(sb.Length, 200)));

        var rowCount = 0;
        while (rowCount < maxRows && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            for (var c = 0; c < colCount; c++)
            {
                if (c > 0)
                {
                    sb.Append(" | ");
                }

                var v = reader.IsDBNull(c) ? "" : reader.GetValue(c)?.ToString() ?? "";
                sb.Append(v.Replace('\n', ' ').Replace('\r', ' '));
            }

            sb.AppendLine();
            rowCount++;
        }

        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sb.AppendLine($"(truncated after {maxRows} rows)");
        }

        return new ContextSlice
        {
            Label = "PostgreSQL",
            Text = sb.ToString()
        };
    }
}
