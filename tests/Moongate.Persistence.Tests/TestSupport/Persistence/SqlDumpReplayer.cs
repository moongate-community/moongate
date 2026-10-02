using System.Text;
using Npgsql;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

/// <summary>
///     Runs an export script the way psql does: statements end with a semicolon, and a
///     <c>COPY ... FROM stdin;</c> statement is followed by its rows up to a <c>\.</c> line.
/// </summary>
public static class SqlDumpReplayer
{
    public static async Task ReplayAsync(string connectionString, string script)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var lines = script.Split('\n');
        var statement = new StringBuilder();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (statement.Length == 0 && (line.Length == 0 || line.StartsWith("--", StringComparison.Ordinal)))
            {
                continue;
            }

            statement.Append(line).Append('\n');

            if (!line.EndsWith(';'))
            {
                continue;
            }

            var sql = statement.ToString().Trim().TrimEnd(';');
            statement.Clear();

            if (!sql.StartsWith("COPY ", StringComparison.Ordinal))
            {
                await using var command = new NpgsqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();

                continue;
            }

            using var import = connection.BeginTextImport(sql.Replace("FROM stdin", "FROM STDIN"));

            for (index++; lines[index] != "\\."; index++)
            {
                await import.WriteAsync(lines[index]);
                await import.WriteAsync('\n');
            }
        }
    }
}
