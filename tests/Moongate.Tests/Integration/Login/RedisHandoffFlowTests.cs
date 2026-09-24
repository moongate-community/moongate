using System.Net;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Tests.Support.Sessions;
using Moongate.Tests.TestSupport.Login;
using Moongate.Tests.TestSupport.Network;
using Moongate.Tests.TestSupport.Packets;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.Integration.Login;

public sealed class RedisHandoffFlowTests
{
    [Fact]
    public async Task LoginSelectionAndGameLogin_TransferAccountThroughOneTimeRedisTicket()
    {
        var endpoint = Environment.GetEnvironmentVariable("MOONGATE_TEST_REDIS_CONNECTION_STRING") ??
                       throw new InvalidOperationException("MOONGATE_TEST_REDIS_CONNECTION_STRING is required.");
        await using var redis = new RedisConnectionService(
            new()
            {
                ConnectionString = endpoint,
                HandoffSecret = new('x', 32)
            }
        );
        await redis.StartAsync();
        using var proof = new HandoffProofService(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
        var prefix = $"test:handoff:realms:{Guid.NewGuid():N}:";
        var catalog = new RedisRealmDirectoryService(redis, prefix);
        var realm = new RealmInstance(
            new(
                "realm-a",
                1,
                "Realm A",
                IPAddress.Loopback,
                2595,
                AccountType.Regular
            ),
            Guid.NewGuid()
        );
        var handoffs = new RedisGameHandoffStore(redis, proof);
        await catalog.RegisterAsync(realm);

        try
        {
            var loginSessions = new LoginSessionService();
            using var loginConnection = new ControlledNetworkConnection(1);
            var loginSession = loginSessions.GetOrCreate(loginConnection);
            var loginSender = new RecordingLoginPacketSender();
            var accounts = new RecordingAccountService
            {
                LoginResult = new() { Id = new(42), AccountType = AccountType.GameMaster }
            };
            var loginHandler = new LoginRoleAccountPacketHandler(
                loginSessions,
                loginSender,
                new(accounts, catalog),
                proof
            );
            await loginHandler.HandleAsync(
                loginSession,
                new("Alice", "password", 0xFF),
                CancellationToken.None
            );
            Assert.IsType<ServerListPacket>(Assert.Single(loginSender.Sent));

            var selectHandler = new LoginRoleServerSelectPacketHandler(
                loginSessions,
                catalog,
                handoffs,
                loginSender
            );
            await selectHandler.HandleAsync(loginSession, new(1), CancellationToken.None);
            var redirect = Assert.IsType<ServerRedirectPacket>(loginSender.Sent[1]);
            Assert.Equal(IPAddress.Loopback, redirect.Address);
            Assert.Equal((ushort)2595, redirect.Port);
            Assert.False(loginConnection.IsConnected);

            await using var gameFixture = await SessionFixture.CreateAsync();
            var gameSessions = new SessionService(gameFixture.Loop);
            var gameSession = gameSessions.GetOrCreate(gameFixture.Client);
            gameSession.NetworkSession.SetSeed(redirect.AuthKey);
            var gameContext = new PacketContext(
                gameSession,
                gameFixture.Loop,
                gameSessions,
                new StubPacketSendService()
            );
            await new GameLoginPacketHandler(realm, handoffs).HandleAsync(
                gameContext,
                new(redirect.AuthKey, "Alice", "password"),
                CancellationToken.None
            );

            Assert.Equal(new(42), gameSession.AccountId);
            Assert.Equal(AccountType.GameMaster, gameSession.AccountType);
            Assert.Null(
                await handoffs.RedeemAsync(
                    realm.Descriptor.RealmId,
                    realm.InstanceId,
                    redirect.AuthKey,
                    "Alice",
                    "password"
                )
            );
        }
        finally
        {
            await catalog.UnregisterAsync(realm);
        }
    }
}
