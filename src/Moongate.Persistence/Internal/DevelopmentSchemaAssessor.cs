using System.Text;
using Moongate.Persistence.Data.Internal;
using Npgsql;

namespace Moongate.Persistence.Internal;

internal static class DevelopmentSchemaAssessor
{
    public static async Task<DevelopmentSchemaAssessment> AssessAsync(
        PostgreSqlDatabase database, Type[] entityTypes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // FreeSql comparison is synchronous; wait for settlement before releasing any schema lock.
        var ddl = await Task.Run(() => database.Orm.CodeFirst.GetComparisonDDLStatements(entityTypes), CancellationToken.None)
                            .ConfigureAwait(false) ?? "";
        cancellationToken.ThrowIfCancellationRequested();
        var newTables = new HashSet<string>(StringComparer.Ordinal);
        var nullableAdditions = new HashSet<string>(StringComparer.Ordinal);
        var removed = new StringBuilder();
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var type in entityTypes)
        {
            var table = database.Orm.CodeFirst.GetTableByEntity(type);
            var parts = table.DbName.Split('.', 2);
            var name = Quote(parts[0]) + "." + Quote(parts[1]);
            await using var command = new NpgsqlCommand(
                "SELECT column_name FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table", connection);
            command.Parameters.AddWithValue("schema", parts[0]);
            command.Parameters.AddWithValue("table", parts[1]);
            var existing = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) { existing.Add(reader.GetString(0)); }
            }
            if (existing.Count == 0) { newTables.Add(name); }
            var columns = table.ColumnsByCs.Values.ToArray();
            foreach (var column in columns.Where(column => column.Attribute.IsNullable && !existing.Contains(column.Attribute.Name)))
            {
                nullableAdditions.Add(name + "." + Quote(column.Attribute.Name));
            }
            foreach (var column in existing.Except(columns.Select(column => column.Attribute.Name), StringComparer.Ordinal))
            {
                // FreeSql retains unmapped database columns; make the removal explicit and review-required.
                removed.AppendLine($"ALTER TABLE {name} DROP COLUMN {Quote(column)};");
            }
        }
        if (!SchemaSqlReader.TryRead(ddl, out var statements)) { return new(ddl + removed, true); }
        var accepted = new StringBuilder();
        var requiresReview = removed.Length > 0;
        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (var statement in statements)
        {
            var tokens = statement.Tokens;
            var text = string.Join(' ', tokens);
            if (tokens.Count == 6 && text.StartsWith("CREATE SCHEMA IF NOT EXISTS ", StringComparison.Ordinal))
            {
                accepted.AppendLine(statement.Sql);
                continue;
            }
            if (tokens.Count > 10 && tokens.Take(5).SequenceEqual(new[] { "CREATE", "TABLE", "IF", "NOT", "EXISTS" }) &&
                newTables.Contains(string.Concat(tokens.Skip(5).Take(3))) && tokens[8] == "(" &&
                !tokens.Any(token => token is "SELECT" or "INSERT" or "UPDATE" or "DELETE" or "DROP" or "ALTER"))
            {
                accepted.AppendLine(statement.Sql);
                continue;
            }
            if (tokens.Count > 8 && tokens.Take(2).SequenceEqual(new[] { "ALTER", "TABLE" }) &&
                tokens[5] == "ADD" && tokens[6] == "COLUMN")
            {
                var column = string.Concat(tokens.Skip(2).Take(3)) + "." + tokens[7];
                // Nullable additions with no default/constraints cannot rewrite existing column values.
                if (nullableAdditions.Contains(column) && IsPlainNullableType(tokens.Skip(8).ToArray()))
                {
                    added.Add(column);
                    accepted.AppendLine(statement.Sql);
                    continue;
                }
            }
            if (tokens.Count == 8 && tokens[0] == "UPDATE" && tokens[4] == "SET" && tokens[6] == "=" && tokens[7] == "NULL" &&
                added.Contains(string.Concat(tokens.Skip(1).Take(3)) + "." + tokens[5]))
            {
                // PostgreSQL initializes a new nullable column to NULL already. Avoid firing UPDATE triggers.
                continue;
            }
            // Comments do not change data, but accept only a literal value and a fixed ON COLUMN/TABLE shape.
            if (tokens.Count >= 7 && tokens[0] == "COMMENT" && tokens[1] == "ON" &&
                tokens[2] is "COLUMN" or "TABLE" && tokens[^2] == "IS" && tokens[^1].StartsWith('\''))
            {
                accepted.AppendLine(statement.Sql);
                continue;
            }
            requiresReview = true;
            accepted.AppendLine(statement.Sql);
        }
        accepted.Append(removed);
        return new(accepted.ToString(), requiresReview);
    }

    private static bool IsPlainNullableType(string[] tokens)
    {
        if (tokens.Length == 0 || tokens[0] is not ("INT2" or "INT4" or "INT8" or "SMALLINT" or "INTEGER" or "BIGINT" or
            "BOOL" or "BOOLEAN" or "VARCHAR" or "CHAR" or "TEXT" or "TIMESTAMP" or "TIMESTAMPTZ" or "DATE" or "TIME" or
            "NUMERIC" or "DECIMAL" or "FLOAT4" or "FLOAT8" or "REAL" or "UUID" or "BYTEA" or "JSON" or "JSONB"))
        {
            return false;
        }
        return tokens.Skip(1).All(token => token is "(" or ")" or "," or "[" or "]" or "NULL" || token.All(char.IsAsciiDigit));
    }

    private static string Quote(string identifier) => '"' + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
