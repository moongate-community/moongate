using Moongate.Server.Data;
using Moongate.Server.Services.Accounts;

namespace Moongate.Tests.Server;

public class PendingLoginStoreTests
{
    [Fact]
    public void Create_ThenTake_ReturnsLoginOnce()
    {
        var now = 1000L;
        var store = new PendingLoginStore(ttlMilliseconds: 5000, nowMilliseconds: () => now);

        var key = store.Create(new PendingLogin("squid"));

        Assert.NotEqual(0u, key);
        Assert.True(store.TryTake(key, out var first));
        Assert.Equal("squid", first.Username);
        Assert.False(store.TryTake(key, out _)); // single-use
    }

    [Fact]
    public void TryTake_UnknownKey_ReturnsFalse()
    {
        var store = new PendingLoginStore(5000, () => 0L);

        Assert.False(store.TryTake(12345u, out _));
    }

    [Fact]
    public void TryTake_ExpiredKey_ReturnsFalse()
    {
        var now = 1000L;
        var store = new PendingLoginStore(ttlMilliseconds: 5000, nowMilliseconds: () => now);

        var key = store.Create(new PendingLogin("squid"));
        now = 1000L + 5001L; // past TTL

        Assert.False(store.TryTake(key, out _));
    }

    [Fact]
    public void Create_ProducesDistinctKeys()
    {
        var store = new PendingLoginStore(5000, () => 0L);

        var a = store.Create(new PendingLogin("a"));
        var b = store.Create(new PendingLogin("b"));

        Assert.NotEqual(a, b);
    }
}
