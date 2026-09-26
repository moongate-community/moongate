using Testcontainers.Redis;

namespace Moongate.Tests.TestSupport.Containers;

/// <summary>
///     The Redis server of the integration tests: the one named by <c>MOONGATE_TEST_REDIS_CONNECTION_STRING</c> when it
///     is set, otherwise a <c>redis:7-alpine</c> container with <c>maxmemory-policy noeviction</c>, as the server
///     requires, started on first use and shared by every test of the process. Testcontainers removes the container
///     when the run ends.
/// </summary>
public static class RedisTestServer
{
    private const string VariableName = "MOONGATE_TEST_REDIS_CONNECTION_STRING";

    private static readonly Lazy<string> _connectionString = new(Resolve);

    /// <summary>
    ///     Gets a StackExchange.Redis connection string.
    /// </summary>
    public static string ConnectionString => _connectionString.Value;

    private static string Resolve()
    {
        var configured = System.Environment.GetEnvironmentVariable(VariableName);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var container = new RedisBuilder("redis:7-alpine").WithCommand("--maxmemory-policy", "noeviction").Build();
        Task.Run(() => container.StartAsync()).GetAwaiter().GetResult();

        return container.GetConnectionString();
    }
}
