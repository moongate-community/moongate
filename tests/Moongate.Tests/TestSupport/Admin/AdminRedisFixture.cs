using System.Security.Cryptography;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Admin;
using Moongate.Server.Services.Redis;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class AdminRedisFixture : IAsyncDisposable
{
    public string Prefix { get; } = $"moongate:admin:test:{Guid.NewGuid():N}:";
    public RedisConnectionService Redis { get; }
    public RedisAdminSessionStore Store { get; }
    public RedisAdminLoginThrottle Throttle { get; }

    private AdminRedisFixture(RedisConnectionService redis)
    {
        Redis = redis;
        Store = new(redis, Prefix);
        Throttle = new(redis, Prefix);
    }

    public static async Task<AdminRedisFixture> CreateAsync()
    {
        var endpoint = System.Environment.GetEnvironmentVariable("MOONGATE_TEST_REDIS_CONNECTION_STRING")
            ?? throw new InvalidOperationException("MOONGATE_TEST_REDIS_CONNECTION_STRING is required.");
        var redis = new RedisConnectionService(new RedisConfig
        {
            ConnectionString = endpoint, HandoffSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
        });
        await redis.StartAsync();
        return new(redis);
    }

    public static string Digest() => Convert.ToHexString(SHA256.HashData(RandomNumberGenerator.GetBytes(32)));

    public async ValueTask DisposeAsync()
    {
        var database = Redis.Connection.GetDatabase();
        foreach (var endpoint in Redis.Connection.GetEndPoints())
        {
            await foreach (var key in Redis.Connection.GetServer(endpoint).KeysAsync(pattern: Prefix + "*"))
            {
                await database.KeyDeleteAsync(key);
            }
        }
        await Redis.DisposeAsync();
    }
}
