using Moongate.Tests.TestSupport.Admin;

namespace Moongate.Tests.Integration.Admin;

public sealed class RedisAdminLoginThrottleTests
{
    [Theory, InlineData(true), InlineData(false)]
    public async Task TryAcquireAsync_SharedLimit_RejectsExcessAttempts(bool sameUsername)
    {
        await using var fixture = await AdminRedisFixture.CreateAsync();
        var limit = sameUsername ? 10 : 30;

        for (var i = 0; i < limit; i++)
        {
            Assert.True(
                await fixture.Throttle.TryAcquireAsync(
                    sameUsername ? $"192.0.2.{i}" : "192.0.2.1",
                    sameUsername ? "alice" : $"user{i}"
                )
            );
        }

        Assert.False(await fixture.Throttle.TryAcquireAsync("192.0.2.1", sameUsername ? "alice" : "other"));
        var database = fixture.Redis.Connection.GetDatabase();

        foreach (var endpoint in fixture.Redis.Connection.GetEndPoints())
        {
            await foreach (var key in fixture.Redis
                               .Connection
                               .GetServer(endpoint)
                               .KeysAsync(pattern: fixture.Prefix + "throttle:*"))
            {
                Assert.DoesNotContain("alice", key.ToString());
                Assert.InRange((await database.KeyTimeToLiveAsync(key))!.Value, TimeSpan.Zero, TimeSpan.FromSeconds(60));
            }
        }
    }
}
