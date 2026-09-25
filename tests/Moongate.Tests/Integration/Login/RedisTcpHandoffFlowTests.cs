using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using DryIoc;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Registry;
using Moongate.Server.Core.Data.Network;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Packets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.GameLoop;
using Moongate.Server.Services.Login;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.Network.Framing;
using Moongate.Server.Services.Packets;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Redis;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Services.Timing;
using Moongate.Server.Ultima.Handlers.Login;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Server.Ultima;
using Moongate.Tests.TestSupport.Ultima.Loaders;

namespace Moongate.Tests.Integration.Login;

public sealed class RedisTcpHandoffFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task TwoListeners_CompleteLoginRedirectAndRejectReplayedGameTicket()
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
        using var proof = new HandoffProofService(RandomNumberGenerator.GetBytes(32));
        var catalog = new RedisRealmDirectoryService(redis, $"test:tcp:realms:{Guid.NewGuid():N}:");
        var handoffs = new RedisGameHandoffStore(redis, proof);

        using var gameContainer = new Container();
        var gameConnections = new ConnectionService();
        var gameSender = new PacketSendService(gameConnections);
        var timers = new TimerWheelService(new(), TimeProvider.System);
        using var loop = new GameLoopService(new(), timers, TimeProvider.System);
        var gameSessions = new SessionService(loop);
        var gameNetwork = CreateNetwork(gameConnections, true);
        var acceptedGameSessions = Channel.CreateUnbounded<long>();
        var gameFrames = Channel.CreateUnbounded<byte[]>();
        gameNetwork.ConnectionAccepted += (_, args) => acceptedGameSessions.Writer.TryWrite(args.Connection.SessionId);
        gameNetwork.DataReceived += (_, args) => gameFrames.Writer.TryWrite(args.Data.ToArray());

        await gameConnections.StartAsync();
        await gameSender.StartAsync();
        await loop.StartAsync();
        await gameNetwork.StartAsync();
        var gameEndpoint = Assert.Single(gameNetwork.Listeners).Endpoint;
        var realm = new RealmInstance(
            new(
                $"tcp-{Guid.NewGuid():N}",
                1,
                "Realm A",
                IPAddress.Loopback,
                (ushort)gameEndpoint.Port,
                AccountType.Regular
            ),
            Guid.NewGuid()
        );
        gameContainer.RegisterInstance<IPacketSendService>(gameSender);
        gameContainer.RegisterInstance(realm);
        gameContainer.RegisterInstance<IGameHandoffStore>(handoffs);
        gameContainer.RegisterInstance<IDataLoaderService>(new StubDataLoaderService());
        gameContainer.RegisterAsyncPacketHandler<GameLoginPacket, GameLoginPacketHandler>();
        var gameDispatcher = new PacketDispatchService(
            loop,
            gameSessions,
            gameContainer.Resolve<PacketHandlerRegistry>(),
            gameContainer
        );
        var gameServer = new GameServerService(gameNetwork, gameConnections, gameSessions, gameDispatcher, gameSender);

        using var loginContainer = new Container();
        var loginConnections = new ConnectionService();
        var loginSender = new PacketSendService(loginConnections);
        var loginSessions = new LoginSessionService();
        var loginNetwork = CreateNetwork(loginConnections, false);
        var accounts = new RecordingAccountService
        {
            LoginResult = new() { Id = new(42), AccountType = AccountType.GameMaster }
        };
        loginContainer.RegisterInstance<ILoginSessionService>(loginSessions);
        loginContainer.RegisterInstance<ILoginPacketSendService>(loginSender);
        loginContainer.RegisterInstance(new LoginAccountFlow(accounts, catalog));
        loginContainer.RegisterInstance<IHandoffProofService>(proof);
        loginContainer.RegisterInstance<IRealmCatalog>(catalog);
        loginContainer.RegisterInstance<IGameHandoffStore>(handoffs);
        loginContainer.RegisterLoginPacketHandler<AccountLoginPacket, LoginRoleAccountPacketHandler>();
        loginContainer.RegisterLoginPacketHandler<ServerSelectPacket, LoginRoleServerSelectPacketHandler>();
        var loginDispatcher = new LoginPacketDispatchService(
            loginSessions,
            loginContainer.Resolve<LoginPacketHandlerRegistry>(),
            loginContainer
        );
        var loginServer = new LoginServerService(
            loginNetwork,
            loginConnections,
            loginSessions,
            loginDispatcher,
            loginSender
        );

        await catalog.RegisterAsync(realm);

        try
        {
            await gameDispatcher.StartAsync();
            await gameServer.StartAsync();
            await loginConnections.StartAsync();
            await loginSender.StartAsync();
            await loginDispatcher.StartAsync();
            await loginServer.StartAsync();
            var loginEndpoint = Assert.Single(loginNetwork.Listeners).Endpoint;
            Assert.NotEqual(loginEndpoint.Port, gameEndpoint.Port);

            using var loginClient = new TcpClient();
            await loginClient.ConnectAsync(loginEndpoint);
            var loginStream = loginClient.GetStream();
            await loginStream.WriteAsync(CreateAccountLogin());
            var serverList = await ReadPacketAsync(loginStream, 0xA8);
            Assert.True(serverList.Length >= 8);
            Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(serverList.AsSpan(4, 2)));
            Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(serverList.AsSpan(6, 2)));
            await loginStream.WriteAsync(new byte[] { 0xA0, 0, 1 });
            var redirect = await ReadPacketAsync(loginStream, 0x8C);
            Assert.Equal(IPAddress.Loopback.GetAddressBytes(), redirect[1..5]);
            Assert.Equal(gameEndpoint.Port, BinaryPrimitives.ReadUInt16BigEndian(redirect.AsSpan(5, 2)));
            var key = BinaryPrimitives.ReadUInt32BigEndian(redirect.AsSpan(7, 4));
            Assert.NotEqual(0u, key);
            Assert.Equal(0, await ReadEofAsync(loginStream));
            Assert.True(
                await redis.Connection.GetDatabase().KeyExistsAsync($"moongate:handoff:{realm.Descriptor.RealmId}:{key:X8}")
            );

            using var gameClient = new TcpClient();
            await gameClient.ConnectAsync(gameEndpoint);
            var firstGameId = await acceptedGameSessions.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            await gameClient.GetStream().WriteAsync(CreateGameLogin(key));
            var seedFrame = await gameFrames.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            Assert.Equal(4, seedFrame.Length);
            Assert.Equal(key, BinaryPrimitives.ReadUInt32BigEndian(seedFrame));
            var loginFrame = await gameFrames.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            Assert.Equal(65, loginFrame.Length);
            Assert.True(PacketRegistry.Default.TryDecode(loginFrame, out var decoded));
            var decodedLogin = Assert.IsType<GameLoginPacket>(decoded);
            Assert.Equal(key, decodedLogin.AuthKey);
            Assert.Equal("Alice", decodedLogin.Account);
            Assert.Equal("password", decodedLogin.Password);
            Assert.True(gameSessions.TryGet(firstGameId, out var acceptedSession));
            Assert.Equal(key, acceptedSession.NetworkSession.Seed);
            await WaitForAccountAsync(gameSessions, firstGameId);
            Assert.True(gameSessions.TryGet(firstGameId, out var gameSession));
            Assert.Equal(new(42), gameSession.AccountId);
            Assert.Equal(AccountType.GameMaster, gameSession.AccountType);
            Assert.False(
                await redis.Connection.GetDatabase().KeyExistsAsync($"moongate:handoff:{realm.Descriptor.RealmId}:{key:X8}")
            );

            using var replayClient = new TcpClient();
            await replayClient.ConnectAsync(gameEndpoint);
            var replayId = await acceptedGameSessions.Reader.ReadAsync().AsTask().WaitAsync(Timeout);
            await replayClient.GetStream().WriteAsync(CreateGameLogin(key));
            var denial = await ReadPacketAsync(replayClient.GetStream(), 0x82);
            Assert.Equal(2, denial.Length);
            Assert.Equal(0, await ReadEofAsync(replayClient.GetStream()));
            Assert.False(gameSessions.TryGet(replayId, out var replaySession) && replaySession.AccountId.IsValid);
        }
        finally
        {
            await loginServer.StopAsync().WaitAsync(Timeout);
            await loginDispatcher.StopAsync().WaitAsync(Timeout);
            await loginSender.StopAsync().WaitAsync(Timeout);
            await loginConnections.StopAsync().WaitAsync(Timeout);
            await gameServer.StopAsync().WaitAsync(Timeout);
            await gameDispatcher.StopAsync().WaitAsync(Timeout);
            await gameSender.StopAsync().WaitAsync(Timeout);
            await loop.StopAsync().WaitAsync(Timeout);
            await gameConnections.StopAsync().WaitAsync(Timeout);
            await catalog.UnregisterAsync(realm);
        }
    }

    private static NetworkService CreateNetwork(ConnectionService connections, bool game)
    {
        return new(
            new NetworkListenerOptions
            {
                Endpoints = [new(IPAddress.Loopback, 0)],
                ConnectionPipelineFactory = () => new()
                {
                    Framer = game ? new GameSeedFramer(PacketRegistry.Default) : new UoPacketFramer(PacketRegistry.Default)
                }
            },
            connections
        );
    }

    private static byte[] CreateAccountLogin()
    {
        var packet = new byte[62];
        packet[0] = 0x80;
        Encoding.ASCII.GetBytes("Alice", packet.AsSpan(1));
        Encoding.ASCII.GetBytes("password", packet.AsSpan(31));
        packet[61] = 0xFF;

        return packet;
    }

    private static byte[] CreateGameLogin(uint key)
    {
        var packet = new byte[69];
        BinaryPrimitives.WriteUInt32BigEndian(packet, key);
        packet[4] = 0x91;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(5), key);
        Encoding.ASCII.GetBytes("Alice", packet.AsSpan(9));
        Encoding.ASCII.GetBytes("password", packet.AsSpan(39));

        return packet;
    }

    private static async Task<byte[]> ReadPacketAsync(NetworkStream stream, byte opcode)
    {
        using var deadline = new CancellationTokenSource(Timeout);
        var header = new byte[opcode == 0xA8 ? 3 :
            opcode == 0x8C ? 11 : 2];
        await stream.ReadExactlyAsync(header, deadline.Token);
        Assert.Equal(opcode, header[0]);

        if (opcode != 0xA8)
        {
            return header;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1));
        Assert.True(length >= header.Length);
        var packet = new byte[length];
        header.CopyTo(packet, 0);
        await stream.ReadExactlyAsync(packet.AsMemory(header.Length), deadline.Token);

        return packet;
    }

    private static async Task<int> ReadEofAsync(NetworkStream stream)
    {
        using var deadline = new CancellationTokenSource(Timeout);
        var byteBuffer = new byte[1];

        return await stream.ReadAsync(byteBuffer, deadline.Token);
    }

    private static async Task WaitForAccountAsync(SessionService sessions, long sessionId)
    {
        using var deadline = new CancellationTokenSource(Timeout);

        while (!deadline.IsCancellationRequested)
        {
            if (sessions.TryGet(sessionId, out var session) && session.AccountId.IsValid)
            {
                return;
            }

            await Task.Delay(10, deadline.Token);
        }

        throw new TimeoutException("Game session did not receive the handoff account.");
    }
}
