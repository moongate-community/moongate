using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Npgsql;

namespace Moongate.Persistence.Internal;

/// <summary>Manages serial sequences through schema migrations and resolves them through column ownership.</summary>
internal static class PersistenceSerialSequence
{
    public static async Task<string> CompareAsync(
        PostgreSqlDatabase database,
        IEnumerable<Type> entityTypes,
        CancellationToken cancellationToken
    )
    {
        var sql = new StringBuilder();
        await using var connection = new NpgsqlConnection(database.SchemaConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var type in entityTypes)
        {
            var table = database.Orm.CodeFirst.GetTableByEntity(type);
            var column = table.ColumnsByCs[nameof(IMoongateEntity.Id)];
            await using var command = new NpgsqlCommand(
                """
                SELECT s.seqmin, s.seqmax, s.seqincrement, s.seqcycle
                FROM pg_sequence s
                WHERE s.seqrelid = CASE WHEN to_regclass(@table) IS NULL THEN NULL
                    ELSE pg_get_serial_sequence(@table, (
                        SELECT attname FROM pg_attribute
                        WHERE attrelid = to_regclass(@table) AND attnum > 0 AND NOT attisdropped
                            AND attname IN (@column, @old_column)
                        ORDER BY (attname = @column) DESC LIMIT 1
                    ))::regclass END
                """,
                connection
            );
            command.Parameters.AddWithValue("table", table.DbName);
            command.Parameters.AddWithValue("column", column.Attribute.Name);
            command.Parameters.AddWithValue("old_column", column.Attribute.OldName ?? column.Attribute.Name);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (reader.GetInt64(0) != 1 ||
                    reader.GetInt64(1) != uint.MaxValue ||
                    reader.GetInt64(2) != 1 ||
                    reader.GetBoolean(3))
                {
                    throw new InvalidOperationException(
                        $"The Serial sequence for '{table.DbName}' must use MINVALUE 1, MAXVALUE {uint.MaxValue}, INCREMENT 1 and NO CYCLE."
                    );
                }

                continue;
            }

            var parts = table.DbName.Split('.', 2);
            var name = parts[1] + "_" + column.Attribute.Name + "_seq";

            if (name.Length > 63)
            {
                var suffix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..16];
                name = name[..46] + "_" + suffix;
            }

            var sequence = Quote(parts[0]) + "." + Quote(name);
            var quotedTable = Quote(parts[0]) + "." + Quote(parts[1]);
            var quotedColumn = Quote(column.Attribute.Name);

            // A DO statement is atomic even in the legacy schema-sync path. Do not adopt unrelated existing objects.
            sql.AppendLine(
                $$"""
                  DO $moongate_serial$
                  DECLARE highest_id bigint;
                  BEGIN
                      LOCK TABLE {{quotedTable}} IN SHARE ROW EXCLUSIVE MODE;
                      SELECT COALESCE(MAX({{quotedColumn}}), 0) INTO highest_id FROM {{quotedTable}};
                      IF highest_id < 0 OR highest_id > {{uint.MaxValue}} THEN
                          RAISE EXCEPTION 'Existing identity is outside the Serial range';
                      END IF;
                      CREATE SEQUENCE {{sequence}} AS bigint MINVALUE 1 MAXVALUE {{uint.MaxValue}} START WITH 1 NO CYCLE;
                      ALTER SEQUENCE {{sequence}} OWNED BY {{quotedTable}}.{{quotedColumn}};
                      PERFORM setval('{{sequence.Replace("'", "''", StringComparison.Ordinal)}}'::regclass,
                          GREATEST(highest_id, 1), highest_id > 0);
                  END
                  $moongate_serial$;
                  """
            );
        }

        return sql.ToString();
    }

    public static async Task<Serial> ReserveAsync<T>(
        IFreeSql orm,
        DbTransaction? transaction,
        CancellationToken cancellationToken
    ) where T : class, IMoongateEntity
    {
        var table = orm.CodeFirst.GetTableByEntity(typeof(T));
        var column = table.ColumnsByCs[nameof(IMoongateEntity.Id)];
        var value = await orm.Ado
                             .CommandFluent(
                                 "SELECT nextval(pg_get_serial_sequence(@table, @column)::regclass)",
                                 new { table = table.DbName, column = column.Attribute.Name }
                             )
                             .WithTransaction(transaction)
                             .ExecuteScalarAsync(cancellationToken)
                             .ConfigureAwait(false);

        if (value is null or DBNull)
        {
            throw new InvalidOperationException(
                $"No Serial sequence is attached to '{table.DbName}.{column.Attribute.Name}'. Generate and apply its SQL migration."
            );
        }

        var serial = Convert.ToInt64(value);

        if (serial <= 0 || serial > uint.MaxValue)
        {
            throw new InvalidOperationException("The sequence returned a value outside the nonzero Serial range.");
        }

        return new((uint)serial);
    }

    private static string Quote(string identifier)
    {
        return '"' + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
