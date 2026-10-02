using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text;
using Moongate.Persistence.Data.Internal;
using Npgsql;
using Serilog;

namespace Moongate.Persistence.Internal;

/// <summary>
///     Writes the data of one PostgreSQL database as a script of <c>COPY ... FROM stdin</c> blocks that psql can
///     run on a database with the same schema. Everything is read in one read-only repeatable-read transaction, so
///     the script is one consistent picture.
/// </summary>
internal static class PostgreSqlDataExporter
{
    // The migration journal is left out: it describes the schema of the database it is in, not the shard's data.
    private const string UserSchemas =
        "n.nspname <> 'information_schema' AND n.nspname <> 'moongate_migrations' AND n.nspname NOT LIKE 'pg\\_%'";

    private const string TablesSql =
        "SELECT c.oid::int8, n.nspname, c.relname, " +
        "pg_catalog.has_schema_privilege(n.oid, 'USAGE') AND pg_catalog.has_table_privilege(c.oid, 'SELECT') " +
        "FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace " +
        "WHERE c.relkind = 'r' AND " + UserSchemas + " ORDER BY n.nspname, c.relname";

    private const string ColumnsSql =
        "SELECT a.attrelid::int8, a.attname FROM pg_catalog.pg_attribute a " +
        "WHERE a.attnum > 0 AND NOT a.attisdropped AND a.attgenerated = '' ORDER BY a.attrelid, a.attnum";

    private const string ReferencesSql =
        "SELECT conrelid::int8, confrelid::int8 FROM pg_catalog.pg_constraint " +
        "WHERE contype = 'f' AND conrelid <> confrelid";

    private const string SequencesSql =
        "SELECT n.nspname, c.relname, " +
        // The CASE keeps the privilege check off the rows that are not sequences, whatever order the filters run in.
        "CASE WHEN c.relkind = 'S' THEN pg_catalog.has_schema_privilege(n.oid, 'USAGE') AND " +
        "pg_catalog.has_sequence_privilege(c.oid, 'SELECT') ELSE false END " +
        "FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace " +
        "WHERE c.relkind = 'S' AND " + UserSchemas + " ORDER BY n.nspname, c.relname";

    private static readonly ILogger _logger = Log.ForContext(typeof(PostgreSqlDataExporter));

    public static async Task ExportAsync(
        string connectionString,
        Stream output,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken
    )
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);

        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection))
        {
            await readOnly.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var skipped = new List<PostgreSqlExportTable>();
        var readable = await ReadTablesAsync(connection, skipped, cancellationToken).ConfigureAwait(false);
        var references = await ReadReferencesAsync(connection, cancellationToken).ConfigureAwait(false);
        var tables = Sort(readable, references);
        var skippedSequences = new List<string>();
        var sequences = await ReadSequencesAsync(connection, skippedSequences, cancellationToken).ConfigureAwait(false);
        var blocking = FindBlockingReferences(readable, skipped, references);

        if (skipped.Count > 0)
        {
            _logger.Warning(
                "SQL export of {Database} skips {Count} tables the runtime role cannot read: {Tables}",
                connection.Database,
                skipped.Count,
                skipped.Select(table => table.QuotedName)
            );
        }

        if (skippedSequences.Count > 0)
        {
            // Without its value in the file, a restore leaves the sequence where the migrations start it.
            _logger.Warning(
                "SQL export of {Database} skips {Count} sequences the runtime role cannot read: {Sequences}",
                connection.Database,
                skippedSequences.Count,
                skippedSequences
            );
        }

        foreach (var warning in blocking)
        {
            _logger.Warning("SQL export of {Database}: {Warning}", connection.Database, warning);
        }

        var writer = new StreamWriter(output, new UTF8Encoding(false), 1 << 16, true) { NewLine = "\n" };

        await using (writer.ConfigureAwait(false))
        {
            await WriteHeaderAsync(writer, connection.Database, createdAt, skipped, skippedSequences, blocking).ConfigureAwait(false);

            // The header reaches the stream first: the snapshot was taken by the catalog queries above.
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            await writer.WriteLineAsync("BEGIN;").ConfigureAwait(false);
            await writer.WriteLineAsync().ConfigureAwait(false);

            if (tables.Count > 0)
            {
                var names = string.Join(", ", tables.Select(table => table.QuotedName));
                await writer.WriteLineAsync($"TRUNCATE TABLE {names};").ConfigureAwait(false);
                await writer.WriteLineAsync().ConfigureAwait(false);
            }

            foreach (var table in tables)
            {
                await WriteTableAsync(connection, writer, table, cancellationToken).ConfigureAwait(false);
            }

            foreach (var sequence in sequences)
            {
                await writer.WriteLineAsync(sequence).ConfigureAwait(false);
            }

            if (sequences.Count > 0)
            {
                await writer.WriteLineAsync().ConfigureAwait(false);
            }

            await writer.WriteLineAsync("COMMIT;").ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Orders tables so a referenced table comes before the tables that reference it, keeping name order
    ///     otherwise. References to a table outside the list are ignored.
    /// </summary>
    internal static IReadOnlyList<PostgreSqlExportTable> Sort(
        IReadOnlyList<PostgreSqlExportTable> tables,
        IReadOnlyList<(long Table, long Referenced)> references
    )
    {
        var pending = tables.ToList();
        var written = new HashSet<long>();
        var known = tables.Select(table => table.Oid).ToHashSet();
        var sorted = new List<PostgreSqlExportTable>(tables.Count);

        while (pending.Count > 0)
        {
            var next = pending.FirstOrDefault(table => references.All(reference =>
                    reference.Table != table.Oid || !known.Contains(reference.Referenced) || written.Contains(reference.Referenced)
                )
            );

            if (next is null)
            {
                throw new InvalidOperationException(
                    "The export cannot order tables that reference each other in a cycle: " +
                    string.Join(", ", pending.Select(table => table.QuotedName)) + "."
                );
            }

            pending.Remove(next);
            written.Add(next.Oid);
            sorted.Add(next);
        }

        return sorted;
    }

    /// <summary>
    ///     Names every skipped table that references an exported one: the restore's TRUNCATE is refused while such a
    ///     table still holds rows.
    /// </summary>
    private static List<string> FindBlockingReferences(
        List<PostgreSqlExportTable> exported,
        List<PostgreSqlExportTable> skipped,
        List<(long Table, long Referenced)> references
    )
    {
        var warnings = new List<string>();

        foreach (var table in skipped)
        {
            foreach (var target in exported)
            {
                if (references.Contains((table.Oid, target.Oid)))
                {
                    warnings.Add(
                        $"skipped {table.QuotedName} references {target.QuotedName}: " +
                        "empty it before the restore, or the TRUNCATE fails."
                    );
                }
            }
        }

        return warnings;
    }

    private static async Task<List<PostgreSqlExportTable>> ReadTablesAsync(
        NpgsqlConnection connection,
        List<PostgreSqlExportTable> skipped,
        CancellationToken cancellationToken
    )
    {
        var tables = new Dictionary<long, PostgreSqlExportTable>();
        var ordered = new List<PostgreSqlExportTable>();

        await using (var command = new NpgsqlCommand(TablesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var table = new PostgreSqlExportTable
                {
                    Oid = reader.GetInt64(0),
                    Schema = reader.GetString(1),
                    Name = reader.GetString(2)
                };

                if (!reader.GetBoolean(3))
                {
                    skipped.Add(table);

                    continue;
                }

                tables.Add(table.Oid, table);
                ordered.Add(table);
            }
        }

        await using (var command = new NpgsqlCommand(ColumnsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (tables.TryGetValue(reader.GetInt64(0), out var table))
                {
                    table.Columns.Add(reader.GetString(1));
                }
            }
        }

        return ordered;
    }

    private static async Task<List<(long Table, long Referenced)>> ReadReferencesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken
    )
    {
        var references = new List<(long Table, long Referenced)>();
        await using var command = new NpgsqlCommand(ReferencesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            references.Add((reader.GetInt64(0), reader.GetInt64(1)));
        }

        return references;
    }

    private static async Task<List<string>> ReadSequencesAsync(
        NpgsqlConnection connection,
        List<string> skipped,
        CancellationToken cancellationToken
    )
    {
        var names = new List<string>();

        await using (var command = new NpgsqlCommand(SequencesSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = PostgreSqlExportTable.Quote(reader.GetString(0)) + "." + PostgreSqlExportTable.Quote(reader.GetString(1));

                // A sequence the role cannot read has no known value: writing one would reset it on restore.
                (reader.GetBoolean(2) ? names : skipped).Add(name);
            }
        }

        var statements = new List<string>(names.Count);

        foreach (var name in names)
        {
            // The sequence itself tells its state; pg_sequences shows no value for one restarted and not used since.
            await using var command = new NpgsqlCommand($"SELECT last_value, is_called FROM {name}", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            var value = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture);
            var called = reader.GetBoolean(1) ? "true" : "false";
            var literal = name.Replace("'", "''", StringComparison.Ordinal);

            statements.Add($"SELECT pg_catalog.setval('{literal}', {value}, {called});");
        }

        return statements;
    }

    private static async Task WriteHeaderAsync(
        StreamWriter writer,
        string? database,
        DateTimeOffset createdAt,
        List<PostgreSqlExportTable> skipped,
        List<string> skippedSequences,
        List<string> warnings
    )
    {
        var version = typeof(PostgreSqlDataExporter).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        await writer.WriteLineAsync("-- Moongate SQL backup").ConfigureAwait(false);
        await writer.WriteLineAsync($"-- Database: {database}").ConfigureAwait(false);
        await writer.WriteLineAsync(
                $"-- Created (UTC): {createdAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}"
            )
            .ConfigureAwait(false);
        await writer.WriteLineAsync($"-- Moongate version: {version}").ConfigureAwait(false);
        await writer.WriteLineAsync(
                "-- Restore: apply the migrations to an empty database, then run this file with psql."
            )
            .ConfigureAwait(false);

        foreach (var table in skipped)
        {
            await writer.WriteLineAsync($"-- Skipped (this role cannot read it): {table.QuotedName}").ConfigureAwait(false);
        }

        foreach (var sequence in skippedSequences)
        {
            await writer.WriteLineAsync($"-- Skipped (this role cannot read it): sequence {sequence}").ConfigureAwait(false);
        }

        foreach (var warning in warnings)
        {
            await writer.WriteLineAsync($"-- Warning: {warning}").ConfigureAwait(false);
        }

        await writer.WriteLineAsync().ConfigureAwait(false);
    }

    private static async Task WriteTableAsync(
        NpgsqlConnection connection,
        StreamWriter writer,
        PostgreSqlExportTable table,
        CancellationToken cancellationToken
    )
    {
        if (table.Columns.Count == 0)
        {
            return;
        }

        var columns = string.Join(", ", table.Columns.Select(PostgreSqlExportTable.Quote));
        await writer.WriteLineAsync($"COPY {table.QuotedName} ({columns}) FROM stdin;").ConfigureAwait(false);

        // Npgsql 5 has only the synchronous call to open the copy; the rows are still read asynchronously.
        using (var rows = connection.BeginTextExport($"COPY {table.QuotedName} ({columns}) TO STDOUT"))
        {
            var buffer = new char[81920];
            int read;

            while ((read = await rows.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await writer.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }

        await writer.WriteLineAsync("\\.").ConfigureAwait(false);
        await writer.WriteLineAsync().ConfigureAwait(false);
    }
}
