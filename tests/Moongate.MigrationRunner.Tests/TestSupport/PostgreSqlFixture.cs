using Moongate.MigrationRunner.Tests.TestSupport.Containers;
using Npgsql;

namespace Moongate.MigrationRunner.Tests.TestSupport;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly string _adminConnectionString;

    public PostgreSqlFixture()
    {
        _adminConnectionString = PostgreSqlTestServer.AdminConnectionString;
    }

    public async Task<PostgreSqlTestDatabase> CreateDatabaseAsync()
    {
        var databaseName = $"moongate_test_{Guid.NewGuid():N}";
        var quotedDatabaseName = new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);

        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"CREATE DATABASE {quotedDatabaseName}",
                connection
            );
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = databaseName,
            Pooling = false
        };

        return new(databaseName, _adminConnectionString, builder.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
