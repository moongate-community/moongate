using System.Net;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Realms;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Realms.Internal;
using Moongate.Server.Services.Redis;

namespace Moongate.Tests.Integration.Realms;

public sealed class RedisRealmDirectoryServiceTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly string _prefix = $"moongate:test:realm:{Guid.NewGuid():N}:";
    private readonly RedisConnectionService _redis = new(new RedisConfig
    {
        ConnectionString = Environment.GetEnvironmentVariable("MOONGATE_TEST_REDIS_CONNECTION_STRING") ?? "localhost:6379",
        HandoffSecret = new string('x', 32)
    });

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
    }

    public async Task DisposeAsync()
    {
        var server = _redis.Connection.GetServer(_redis.Connection.GetEndPoints()[0]);
        var keys = server.Keys(pattern: _prefix + "*").ToArray();
        if (keys.Length > 0)
        {
            await _redis.Connection.GetDatabase().KeyDeleteAsync(keys);
        }

        await _redis.StopAsync();
    }

    [Fact]
    public async Task RegisterAsync_PublishesTwoLiveRealmsSortedAndFilteredByAccountLevel()
    {
        var directory = Create();
        await directory.RegisterAsync(Realm("staff", 4, AccountType.GameMaster));
        await directory.RegisterAsync(Realm("public", 2));

        Assert.Equal([2], (await directory.GetAvailableAsync(AccountType.Regular)).Select(realm => realm.ServerIndex));
        Assert.Equal([2, 4], (await directory.GetAvailableAsync(AccountType.GameMaster))
            .Select(realm => realm.ServerIndex));
        Assert.Equal("staff", (await directory.FindByIndexAsync(4, AccountType.GameMaster))!.Descriptor.RealmId);
        Assert.Null(await directory.FindByIndexAsync(4, AccountType.Regular));
    }

    [Fact]
    public async Task RegisterAsync_DifferentRealmCannotClaimAnOccupiedIndex()
    {
        var directory = Create();
        await directory.RegisterAsync(Realm("first", 7));

        var exception = await Assert.ThrowsAsync<RealmDirectoryException>(
            () => directory.RegisterAsync(Realm("second", 7)).AsTask()
        );

        Assert.Equal(RealmRegistrationError.DuplicateIndex, exception.Error);
        Assert.Equal("first", (await directory.FindByIndexAsync(7, AccountType.Regular))!.Descriptor.RealmId);
    }

    [Fact]
    public async Task ReplacementInstance_FencesOldRenewAndUnregister()
    {
        var directory = Create();
        var first = Realm("same", 9);
        var replacement = Realm("same", 9);
        await directory.RegisterAsync(first);
        await directory.RegisterAsync(replacement);

        Assert.False(await directory.RenewAsync(first));
        await directory.UnregisterAsync(first);
        Assert.Equal(replacement.InstanceId,
            (await directory.FindByIndexAsync(9, AccountType.Regular))!.InstanceId);
        Assert.True(await directory.RenewAsync(replacement));
    }

    [Fact]
    public async Task RegisterAsync_ExpiresAfterLeaseAndRenewRefreshesTtl()
    {
        var directory = Create();
        var realm = Realm("timed", 10);
        await directory.RegisterAsync(realm);
        var database = _redis.Connection.GetDatabase();
        var ttl = await database.KeyTimeToLiveAsync(_prefix + "10");
        Assert.InRange(ttl!.Value.TotalSeconds, 10, 15);

        await database.KeyExpireAsync(_prefix + "10", TimeSpan.FromMilliseconds(50));
        Assert.True(await directory.RenewAsync(realm));
        Assert.InRange((await database.KeyTimeToLiveAsync(_prefix + "10"))!.Value.TotalSeconds, 10, 15);

        await database.KeyExpireAsync(_prefix + "10", TimeSpan.FromMilliseconds(50));
        await Task.Delay(150);
        Assert.Empty(await directory.GetAvailableAsync(AccountType.Regular));
        Assert.False(await directory.RenewAsync(realm));
    }

    [Fact]
    public async Task GetAvailableAsync_SkipsMalformedOrVanishedHashes()
    {
        var directory = Create();
        await directory.RegisterAsync(Realm("valid", 1));
        await _redis.Connection.GetDatabase().HashSetAsync(_prefix + "2", "realm_id", "incomplete");
        await _redis.Connection.GetDatabase().HashSetAsync(_prefix + "3", "port", "not-a-port");
        await _redis.Connection.GetDatabase().KeyExpireAsync(_prefix + "3", TimeSpan.FromMilliseconds(20));
        await Task.Delay(70);

        Assert.Equal([1], (await directory.GetAvailableAsync(AccountType.Regular))
            .Select(realm => realm.ServerIndex));
    }

    [Fact]
    public async Task RegisterAsync_DefaultCapacityRejectsThe129thRealm()
    {
        var directory = Create();
        for (ushort index = 0; index < 128; index++)
        {
            await directory.RegisterAsync(Realm($"realm-{index}", index));
        }

        var exception = await Assert.ThrowsAsync<RealmDirectoryException>(
            () => directory.RegisterAsync(Realm("overflow", 128)).AsTask()
        );

        Assert.Equal(RealmRegistrationError.CapacityExceeded, exception.Error);
        Assert.Equal(128, (await directory.GetAvailableAsync(AccountType.Regular)).Count);
    }

    private RedisRealmDirectoryService Create()
        => new(_redis, _prefix, TimeSpan.FromSeconds(15), 128);

    private static RealmInstance Realm(string id, ushort index, AccountType minimum = AccountType.Regular)
        => new(new RealmDescriptor(id, index, id, IPAddress.Loopback, 2595, minimum), Guid.NewGuid());

    ValueTask IAsyncDisposable.DisposeAsync()
        => new(DisposeAsync());
}
