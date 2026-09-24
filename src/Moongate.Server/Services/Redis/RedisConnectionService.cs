using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Data.Config.Sections;
using StackExchange.Redis;

namespace Moongate.Server.Services.Redis;

/// <summary>Owns one Redis multiplexer for the process lifetime.</summary>
public sealed class RedisConnectionService : IMoongateStartupService, IAsyncDisposable
{
    private readonly RedisConfig _config;
    private IConnectionMultiplexer? _connection;

    public IConnectionMultiplexer Connection
        => _connection ?? throw new InvalidOperationException("The Redis connection has not started.");

    public RedisConnectionService(RedisConfig config)
    {
        _config = config;
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        if (_connection is not null)
        {
            return;
        }

        var connectionString = _config.ResolveConnectionString();
        _config.ResolveHandoffSecret();

        try
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = true;
            options.ConnectRetry = 0;
            options.ConnectTimeout = 1500;
            options.AsyncTimeout = 1500;
            var connection = await ConnectionMultiplexer.ConnectAsync(options).ConfigureAwait(false);

            try
            {
                await connection.GetDatabase().PingAsync().ConfigureAwait(false);
                _connection = connection;
            }
            catch
            {
                await connection.CloseAsync().ConfigureAwait(false);
                connection.Dispose();

                throw;
            }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Unable to connect to redis.connection_string.");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        var connection = Interlocked.Exchange(ref _connection, null);

        if (connection is null)
        {
            return;
        }

        await connection.CloseAsync().ConfigureAwait(false);
        connection.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
        => await StopAsync().ConfigureAwait(false);
}
