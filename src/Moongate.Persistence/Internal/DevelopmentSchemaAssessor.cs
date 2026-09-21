using System.Text;
using Moongate.Persistence.Data.Internal;
using Npgsql;

namespace Moongate.Persistence.Internal;

internal static class DevelopmentSchemaAssessor
{
    public static async Task<DevelopmentSchemaAssessment> AssessAsync(
        PostgreSqlDatabase database, Type[] entityTypes, CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // FreeSql comparison is synchronous; wait for settlement before releasing any schema lock.
        var ddl = await Task.Run(
                () => database.Orm.CodeFirst.GetComparisonDDLStatements(entityTypes),
                CancellationToken.None
            )
            .ConfigureAwait(false) ?? "";
        cancellationToken.ThrowIfCancellationRequested();
        var parsed = SchemaSqlReader.TryRead(ddl, out var statements);
        var hasExistingTables = false;
        var newTables = new HashSet<string>(StringComparer.Ordinal);
        var nullableAdditions = new HashSet<string>(StringComparer.Ordinal);
        var literalDefaults = new Dictionary<string, (string Value, bool Nullable)>(StringComparer.Ordinal);
        var removed = new StringBuilder();
        await using var connection = new NpgsqlConnection(database.RuntimeConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var type in entityTypes)
        {
            var table = database.Orm.CodeFirst.GetTableByEntity(type);
            var parts = table.DbName.Split('.', 2);
            var name = Quote(parts[0]) + "." + Quote(parts[1]);
            await using var command = new NpgsqlCommand(
                "SELECT column_name, column_default FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table",
                connection
            );
            command.Parameters.AddWithValue("schema", parts[0]);
            command.Parameters.AddWithValue("table", parts[1]);
            var existing = new Dictionary<string, string?>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    existing.Add(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
                }
            }

            if (existing.Count == 0)
            {
                newTables.Add(name);
            }
            else
            {
                hasExistingTables = true;
            }

            var columns = table.ColumnsByCs.Values.ToArray();
            foreach (var column in columns.Where(column =>
                         column.Attribute.IsNullable && !existing.ContainsKey(column.Attribute.Name)
                     ))
            {
                nullableAdditions.Add(name + "." + Quote(column.Attribute.Name));
            }

            foreach (var column in columns.Where(column => !existing.ContainsKey(column.Attribute.Name)))
            {
                var value = GetLiteralDefault(column.Attribute.DbType);
                if (value is not null)
                {
                    literalDefaults.Add(name + "." + Quote(column.Attribute.Name), (value, column.Attribute.IsNullable));
                }
            }

            var renamed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in columns)
            {
                var attribute = column.Attribute;
                var oldName = attribute.OldName;
                var isRename = parsed && !string.IsNullOrEmpty(oldName) && statements.Any(statement =>
                    statement.Tokens.SequenceEqual(
                        new[]
                        {
                            "ALTER", "TABLE", Quote(parts[0]), ".", Quote(parts[1]), "RENAME", "COLUMN",
                            Quote(oldName), "TO", Quote(attribute.Name)
                        }
                    )
                );
                if (isRename)
                {
                    renamed.Add(oldName!);
                }

                if (!attribute.IsIdentity && existing.TryGetValue(
                        isRename ? oldName! : attribute.Name,
                        out var actualDefault
                    ))
                {
                    var declaredDefault = GetDefaultExpression(attribute.DbType);
                    if (!DefaultsMatch(declaredDefault, actualDefault))
                    {
                        removed.AppendLine(
                            $"ALTER TABLE {name} ALTER COLUMN {Quote(attribute.Name)} " +
                            (declaredDefault is null ? "DROP DEFAULT;" : $"SET DEFAULT {declaredDefault};")
                        );
                    }
                }
            }

            foreach (var column in existing.Keys.Except(
                             columns.Select(column => column.Attribute.Name),
                             StringComparer.Ordinal
                         )
                         .Except(renamed))
            {
                // FreeSql retains unmapped database columns; make the removal explicit and review-required.
                removed.AppendLine($"ALTER TABLE {name} DROP COLUMN {Quote(column)};");
            }
        }

        if (!parsed)
        {
            return new(ddl + removed, true, hasExistingTables);
        }

        var accepted = new StringBuilder();
        var requiresReview = removed.Length > 0;
        var added = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < statements.Count; index++)
        {
            var statement = statements[index];
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
                if (literalDefaults.TryGetValue(column, out var declared) && IsPlainNullableType(tokens.Skip(8).ToArray()) &&
                    index + 1 < statements.Count)
                {
                    var update = statements[index + 1].Tokens;
                    var expectedUpdate = new[]
                        { "UPDATE", tokens[2], tokens[3], tokens[4], "SET", tokens[7], "=", declared.Value };
                    var expectedConstraint = new[]
                    {
                        "ALTER", "TABLE", tokens[2], tokens[3], tokens[4], "ALTER", "COLUMN", tokens[7], "SET", "NOT", "NULL"
                    };
                    if (update.SequenceEqual(expectedUpdate) &&
                        (declared.Nullable || index + 2 < statements.Count &&
                            statements[index + 2].Tokens.SequenceEqual(expectedConstraint)))
                    {
                        // Add with its declared constant default in one statement, without firing UPDATE triggers.
                        accepted.Append(string.Join(' ', tokens)).Append(" DEFAULT ").Append(declared.Value);
                        accepted.AppendLine(declared.Nullable ? ";" : " NOT NULL;");
                        index += declared.Nullable ? 1 : 2;
                        continue;
                    }
                }

                // Nullable additions with no default/constraints cannot rewrite existing column values.
                if (nullableAdditions.Contains(column) && IsPlainNullableType(tokens.Skip(8).ToArray()))
                {
                    added.Add(column);
                    accepted.AppendLine(statement.Sql);
                    continue;
                }
            }

            if (tokens.Count == 8 && tokens[0] == "UPDATE" && tokens[4] == "SET" && tokens[6] == "=" &&
                tokens[7] == "NULL" &&
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
        return new(accepted.ToString(), requiresReview, hasExistingTables);
    }

    private static string? GetDefaultExpression(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType))
        {
            return null;
        }

        if (!SchemaSqlReader.TryRead(dbType, out var statements) || statements.Count != 1)
        {
            throw new InvalidOperationException(
                $"Cannot assess column default in DbType '{dbType}'. Use a reviewed SQL migration."
            );
        }

        var statement = statements[0];
        var index = statement.Tokens.ToList().IndexOf("DEFAULT");
        if (index < 0)
        {
            return null;
        }

        if (index + 1 == statement.Tokens.Count)
        {
            throw new InvalidOperationException("A column DEFAULT must include an expression.");
        }

        var end = statement.Sql.TrimEnd().TrimEnd(';').Length;
        if (statement.Tokens.Count > index + 3 && statement.Tokens[^2] == "NOT" && statement.Tokens[^1] == "NULL")
        {
            end = statement.TokenOffsets[^2];
        }

        return statement.Sql[statement.TokenOffsets[index + 1]..end].Trim();
    }

    private static bool DefaultsMatch(string? declared, string? actual)
    {
        return NormalizeDefault(declared).SequenceEqual(NormalizeDefault(actual));
    }

    private static string[] NormalizeDefault(string? value)
    {
        if (value is null)
        {
            return [];
        }

        if (!SchemaSqlReader.TryRead(value, out var statements) || statements.Count != 1)
        {
            return [value];
        }

        var tokens = statements[0].Tokens.ToList();
        // PostgreSQL renders a string constant with its inferred type cast.
        if (tokens.Count > 3 && tokens[0].StartsWith('\'') && tokens[1] == ":" && tokens[2] == ":" &&
            tokens.Skip(3).All(token => token is "CHARACTER" or "VARYING" or "TEXT" or "VARCHAR" or "BPCHAR"))
        {
            tokens.RemoveRange(1, tokens.Count - 1);
        }

        return tokens.ToArray();
    }

    private static string? GetLiteralDefault(string? dbType)
    {
        if (string.IsNullOrWhiteSpace(dbType) || !SchemaSqlReader.TryRead(dbType, out var statements) ||
            statements.Count != 1)
        {
            return null;
        }

        var tokens = statements[0].Tokens.ToList();
        var index = tokens.IndexOf("DEFAULT");
        if (index < 1 || index != tokens.Count - 2)
        {
            return null;
        }

        var prefix = tokens.Take(index).ToList();
        if (prefix.Count >= 2 && prefix[^2] == "NOT" && prefix[^1] == "NULL")
        {
            prefix.RemoveRange(prefix.Count - 2, 2);
        }

        var value = tokens[^1];
        return IsPlainNullableType(prefix.ToArray()) &&
               (value is "TRUE" or "FALSE" || value.All(char.IsAsciiDigit) || value.StartsWith('\''))
            ? value
            : null;
    }

    private static bool IsPlainNullableType(string[] tokens)
    {
        if (tokens.Length == 0 || tokens[0] is not ("INT2" or "INT4" or "INT8" or "SMALLINT" or "INTEGER" or "BIGINT" or
                "BOOL" or "BOOLEAN" or "VARCHAR" or "CHAR" or "TEXT" or "TIMESTAMP" or "TIMESTAMPTZ" or "DATE" or "TIME" or
                "NUMERIC" or "DECIMAL" or "FLOAT4" or "FLOAT8" or "REAL" or "UUID" or "BYTEA" or "JSON" or "JSONB"))
        {
            return false;
        }

        return tokens.Skip(1)
            .All(token => token is "(" or ")" or "," or "[" or "]" or "NULL" || token.All(char.IsAsciiDigit));
    }

    private static string Quote(string identifier) => '"' + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
}
