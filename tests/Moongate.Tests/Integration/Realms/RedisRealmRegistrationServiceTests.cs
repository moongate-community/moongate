using System.Net;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Data.Config.Sections;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Tests.TestSupport.Containers;

namespace Moongate.Tests.Integration.Realms;

public sealed class RedisRealmRegistrationServiceTests
{
    [Fact]
    public async Task StartAsync_PublishesBeforeReturningAndStopRemovesOwnedLease()
    {
        await using var redis = await CreateConnectionAsync();
        var prefix = Prefix();
        var directory = new RedisRealmDirectoryService(redis, prefix, TimeSpan.FromSeconds(4));
        var realm = Realm();
        await using var registration = new RedisRealmRegistrationService(directory, realm, Config(), TimeProvider.System);

        await registration.StartAsync();
        Assert.Equal(
            realm.InstanceId,
            (await directory.FindByIndexAsync(5, AccountType.Regular))!.InstanceId
        );

        await registration.StopAsync();
        Assert.Null(await directory.FindByIndexAsync(5, AccountType.Regular));
    }

    [Fact]
    public async Task RenewAsync_MissingLeaseIsRepublishedOnNextHeartbeat()
    {
        await using var redis = await CreateConnectionAsync();
        var prefix = Prefix();
        var directory = new RedisRealmDirectoryService(redis, prefix, TimeSpan.FromSeconds(4));
        var realm = Realm();
        await using var registration = new RedisRealmRegistrationService(directory, realm, Config(), TimeProvider.System);

        await registration.StartAsync();
        await redis.Connection.GetDatabase().KeyDeleteAsync(prefix + "5");
        await WaitForAsync(async () => (await directory.FindByIndexAsync(5, AccountType.Regular))?.InstanceId ==
                                       realm.InstanceId
        );

        await registration.StopAsync();
    }

    [Fact]
    public async Task RenewAsync_ReplacedInstanceDoesNotReclaimOrUnregisterItsSuccessor()
    {
        await using var redis = await CreateConnectionAsync();
        var prefix = Prefix();
        var directory = new RedisRealmDirectoryService(redis, prefix, TimeSpan.FromSeconds(4));
        var original = Realm();
        var successor = Realm();
        await using var registration = new RedisRealmRegistrationService(directory, original, Config(), TimeProvider.System);

        try
        {
            await registration.StartAsync();
            await directory.RegisterAsync(successor);
            await Task.Delay(TimeSpan.FromSeconds(1.3));
            Assert.Equal(
                successor.InstanceId,
                (await directory.FindByIndexAsync(5, AccountType.Regular))!.InstanceId
            );
            Assert.True(await redis.Connection.GetDatabase().KeyDeleteAsync(prefix + "5"));
            await Task.Delay(TimeSpan.FromSeconds(1.3));
            Assert.Null(await directory.FindByIndexAsync(5, AccountType.Regular));
            await registration.StopAsync();
        }
        finally
        {
            await redis.Connection.GetDatabase().KeyDeleteAsync(prefix + "5");
        }
    }

    private static RealmDirectoryConfig Config()
    {
        return new() { HeartbeatIntervalSeconds = 1, LeaseDurationSeconds = 4 };
    }

    private static RealmInstance Realm()
    {
        return new(
            new(
                "realm",
                5,
                "Realm",
                IPAddress.Loopback,
                2595,
                AccountType.Regular
            ),
            Guid.NewGuid()
        );
    }

    private static string Prefix()
    {
        return $"moongate:test:registration:{Guid.NewGuid():N}:";
    }

    private static async Task<RedisConnectionService> CreateConnectionAsync()
    {
        var redis = new RedisConnectionService(
            new()
            {
                ConnectionString = RedisTestServer.ConnectionString,
                HandoffSecret = new('x', 32)
            }
        );
        await redis.StartAsync();

        return redis;
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!await condition())
        {
            await Task.Delay(50, timeout.Token);
        }
    }
}
