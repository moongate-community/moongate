using Moongate.Server.Services.Redis;

namespace Moongate.Tests.Integration.Redis;

public sealed class RedisConnectionServiceTests
{
    [Fact]
    public async Task StartAsync_PingsSharedRedisAndReleasesConnectionOnStop()
    {
        var endpoint = Environment.GetEnvironmentVariable("MOONGATE_TEST_REDIS_CONNECTION_STRING") ?? "localhost:6379";
        var service = new RedisConnectionService(
            new()
            {
                ConnectionString = endpoint,
                HandoffSecret = new('x', 32)
            }
        );

        await service.StartAsync();
        Assert.True(service.Connection.IsConnected);
        Assert.True(await service.Connection.GetDatabase().PingAsync() >= TimeSpan.Zero);

        await service.StopAsync();
        Assert.Throws<InvalidOperationException>(() => service.Connection);
    }

    [Fact]
    public async Task StartAsync_UnavailableEndpointFailsWithinBoundedTimeWithoutLeakingSettings()
    {
        var service = new RedisConnectionService(
            new()
            {
                ConnectionString = "127.0.0.1:1,password=not-for-errors",
                HandoffSecret = new('x', 32)
            }
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                            () => service.StartAsync().WaitAsync(TimeSpan.FromSeconds(5))
                        );

        Assert.Contains("redis.connection_string", exception.Message);
        Assert.DoesNotContain("not-for-errors", exception.ToString());
        await service.StopAsync();
    }
}
