using Npgsql;

namespace Moongate.Persistence.Internal;

internal sealed class PostgreSqlSchemaLockLease : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private bool _disposed;

    public PostgreSqlSchemaLockLease(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task ExecuteDdlAsync(string ddl, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(ddl);
        await using var command = new NpgsqlCommand(ddl, _connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
