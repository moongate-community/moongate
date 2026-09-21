using DbUp.Postgresql;
using Moongate.Persistence.Migrations.Data.Migrations;

namespace Moongate.MigrationRunner.Internal;

internal static class TransactionalSql
{
    public static void Validate(MigrationScript script)
    {
        // Use the same PostgreSQL parser as execution, including dollar quotes and escaped strings.
        var parser = new PostgresqlConnectionManager(string.Empty);

        foreach (var statement in parser.SplitScriptIntoCommands(script.Sql))
        {
            var sql = SkipComments(statement.AsSpan());
            var end = 0;

            while (end < sql.Length && char.IsAsciiLetter(sql[end]))
            {
                end++;
            }

            var keyword = sql[..end].ToString().ToUpperInvariant();

            if (keyword is "BEGIN" or
                           "START" or
                           "COMMIT" or
                           "END" or
                           "ROLLBACK" or
                           "ABORT" or
                           "SAVEPOINT" or
                           "RELEASE" or
                           "PREPARE" or
                           "SET" or
                           "RESET" or
                           "DISCARD")
            {
                throw new InvalidOperationException(
                    $"Migration '{script.Name}' contains transaction or session control. Migrations must run inside the runner transaction."
                );
            }
        }
    }

    private static ReadOnlySpan<char> SkipComments(ReadOnlySpan<char> sql)
    {
        while (true)
        {
            sql = sql.TrimStart();

            if (sql.StartsWith("--", StringComparison.Ordinal))
            {
                var newline = sql.IndexOfAny('\r', '\n');
                sql = newline < 0 ? [] : sql[(newline + 1)..];
            }
            else if (sql.StartsWith("/*", StringComparison.Ordinal))
            {
                var depth = 1;
                var index = 2;

                while (index + 1 < sql.Length && depth > 0)
                {
                    if (sql[index..].StartsWith("/*", StringComparison.Ordinal))
                    {
                        depth++;
                        index += 2;
                    }
                    else if (sql[index..].StartsWith("*/", StringComparison.Ordinal))
                    {
                        depth--;
                        index += 2;
                    }
                    else
                    {
                        index++;
                    }
                }

                sql = depth == 0 ? sql[index..] : [];
            }
            else
            {
                return sql;
            }
        }
    }
}
