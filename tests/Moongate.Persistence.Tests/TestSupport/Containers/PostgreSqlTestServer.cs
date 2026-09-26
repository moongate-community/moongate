using Npgsql;
using Testcontainers.PostgreSql;

namespace Moongate.Persistence.Tests.TestSupport.Containers;

/// <summary>
///     The PostgreSQL server of the integration tests: the one named by
///     <c>MOONGATE_TEST_POSTGRES_CONNECTION_STRING</c> when it is set, otherwise a <c>postgres:17-alpine</c> container
///     started on first use and shared by every test of the process. Testcontainers removes the container when the run
///     ends.
/// </summary>
public static class PostgreSqlTestServer
{
    private const string VariableName = "MOONGATE_TEST_POSTGRES_CONNECTION_STRING";

    private static readonly Lazy<string> _adminConnectionString = new(Resolve);

    /// <summary>
    ///     Gets an administrative connection string whose role may create and drop databases.
    /// </summary>
    public static string AdminConnectionString => _adminConnectionString.Value;

    private static string Resolve()
    {
        var configured = Environment.GetEnvironmentVariable(VariableName);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        Task.Run(() => container.StartAsync()).GetAwaiter().GetResult();

        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Pooling = false }.ConnectionString;
    }
}
