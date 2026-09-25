using System.Net;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;

namespace Moongate.Tests.Integration.Login;

public sealed class RealmDirectoryProcessTests
{
    [Fact]
    public async Task RedisCatalog_SeesTwoGameProcessesAcrossLoginRestartAndExpiresLostLease()
    {
        var prefix = $"moongate:test:process:{Guid.NewGuid():N}:";
        await using var gameA = await ConnectAsync();
        await using var gameB = await ConnectAsync();
        await using var login = await ConnectAsync();
        var gameADirectory = new RedisRealmDirectoryService(gameA, prefix, TimeSpan.FromSeconds(3));
        var gameBDirectory = new RedisRealmDirectoryService(gameB, prefix, TimeSpan.FromSeconds(3));
        var liveRealm = CreateRealm("live", 3);
        var lostRealm = CreateRealm("lost", 7);
        await using var registration = new RedisRealmRegistrationService(
            gameADirectory,
            liveRealm,
            new() { HeartbeatIntervalSeconds = 1, LeaseDurationSeconds = 3 },
            TimeProvider.System
        );

        try
        {
            await registration.StartAsync();
            await gameBDirectory.RegisterAsync(lostRealm);
            IRealmCatalog catalog = new RedisRealmDirectoryService(login, prefix, TimeSpan.FromSeconds(3));
            Assert.Equal(
                [3, 7],
                (await catalog.GetAvailableAsync(AccountType.Regular))
                .Select(realm => realm.ServerIndex)
            );

            await login.StopAsync();
            await using var restartedLogin = await ConnectAsync();
            catalog = new RedisRealmDirectoryService(restartedLogin, prefix, TimeSpan.FromSeconds(3));
            Assert.Equal(
                [3, 7],
                (await catalog.GetAvailableAsync(AccountType.Regular))
                .Select(realm => realm.ServerIndex)
            );

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            while (await catalog.FindByIndexAsync(7, AccountType.Regular, timeout.Token) is not null)
            {
                await Task.Delay(50, timeout.Token);
            }

            Assert.Equal(
                [3],
                (await catalog.GetAvailableAsync(AccountType.Regular))
                .Select(realm => realm.ServerIndex)
            );
            Assert.Equal(
                liveRealm.InstanceId,
                (await catalog.FindByIndexAsync(3, AccountType.Regular))!.InstanceId
            );
        }
        finally
        {
            await registration.StopAsync();
            await gameA.Connection.GetDatabase().KeyDeleteAsync(prefix + "3");
            await gameA.Connection.GetDatabase().KeyDeleteAsync(prefix + "7");
        }
    }

    private static RealmInstance CreateRealm(string id, ushort index)
    {
        return new(
                new(id, index, id, IPAddress.Loopback, 2595, AccountType.Regular),
                Guid.NewGuid()
            );
    }

    private static async Task<RedisConnectionService> ConnectAsync()
    {
        var redis = new RedisConnectionService(
            new()
            {
                ConnectionString = Environment.GetEnvironmentVariable("MOONGATE_TEST_REDIS_CONNECTION_STRING") ??
                                   "localhost:6379",
                HandoffSecret = new('x', 32)
            }
        );
        await redis.StartAsync();

        return redis;
    }
}
