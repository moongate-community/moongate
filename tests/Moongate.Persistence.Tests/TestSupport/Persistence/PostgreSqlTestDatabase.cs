using Npgsql;

namespace Moongate.Persistence.Tests.TestSupport.Persistence;

public sealed class PostgreSqlTestDatabase : IAsyncDisposable
{
    private readonly string _databaseName;
    private readonly string _adminConnectionString;
    private bool _disposed;

    public string ConnectionString { get; }

    public PostgreSqlTestDatabase(string databaseName, string adminConnectionString, string connectionString)
    {
        if (!databaseName.StartsWith("moongate_test_", StringComparison.Ordinal))
        {
            throw new ArgumentException("Test database names must use the moongate_test_ prefix.", nameof(databaseName));
        }

        _databaseName = databaseName;
        _adminConnectionString = adminConnectionString;
        ConnectionString = connectionString;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();

        return result is null or DBNull ? default : (T)result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var quotedDatabaseName = new NpgsqlCommandBuilder().QuoteIdentifier(_databaseName);
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE {quotedDatabaseName} WITH (FORCE)",
            connection);
        await command.ExecuteNonQueryAsync();
    }
}
