using Moongate.Persistence.Types.Persistence;
using Npgsql;

namespace Moongate.Persistence.Internal;

internal static class PostgreSqlSchemaLock
{
    private const int LockKeyBase = 0x4D470000;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    public static async Task<PostgreSqlSchemaLockLease> AcquireAsync(
        string connectionString,
        PersistenceDatabaseTarget target,
        CancellationToken cancellationToken
    )
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false
        };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var command = new NpgsqlCommand(
                    "SELECT pg_try_advisory_lock(" +
                    "(SELECT oid::integer FROM pg_database WHERE datname = current_database()), @lock_key)",
                    connection
                );
                command.Parameters.AddWithValue("lock_key", LockKeyBase + (int)target);
                if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true)
                {
                    return new PostgreSqlSchemaLockLease(connection);
                }

                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
