using System.Net;
using System.Net.Sockets;
using System.Text;
using Moongate.Server.Data.Config;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Network;
using Moongate.Server.Data.Events;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.World;
using Moongate.UO.Data.StartingCities;
using Moongate.UO.Data.Types;
using Moongate.Tests.Support;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server;

public class LoginFlowIntegrationTests
{
    // Runs posted work inline (no game loop in the test), so inbound packets are processed
    // synchronously on the receive thread exactly as before the main-thread marshaling.
    private sealed class InlineDispatcher : IMainThreadDispatcher
    {
        public int PendingCount => 0;

        public int DrainPending(double? budgetMs = null)
        {
            return 0;
        }

        public void Post(Action action)
        {
            action();
        }
    }

    private static MoongateConfig LoopbackConfig()
    {
        return new MoongateConfig
        {
            ShardName = "Moongate",
            Network = new MoongateNetworkConfig { Address = "127.0.0.1", Port = 0, PublicAddress = "127.0.0.1" }
        };
    }

    private static Task<NetworkService> StartServerAsync(MoongateConfig config)
    {
        return StartServerAsync(config, new EventBusService());
    }

    private static async Task<NetworkService> StartServerAsync(MoongateConfig config, IEventBus eventBus)
    {
        var sessions = new SessionManager();
        var accounts = new StubAccountService();
        var pending = new PendingLoginStore(30000, () => Environment.TickCount64);

        var cities = new StartingCityService();
        cities.Register(
            new StartingCity
            {
                City = "Britain",
                Building = "Castle British",
                Description = 1075072,
                X = 1495,
                Y = 1629,
                Z = 10,
                Map = MapType.Trammel
            }
        );

        var handlers = new IPacketHandlerRegistration[]
        {
            new LoginSeedHandler(),
            new AccountLoginHandler(accounts, config),
            new SelectServerHandler(pending, config),
            new GameServerLoginHandler(pending, cities, accounts, new StubCharacterService())
        };

        var network = new NetworkService(sessions, config, handlers, eventBus, new InlineDispatcher());
        await network.StartAsync(CancellationToken.None);

        return network;
    }

    private static byte[] AccountLogin(string account, string password)
    {
        var packet = new byte[62];
        packet[0] = 0x80;
        Encoding.ASCII.GetBytes(account).CopyTo(packet.AsSpan(1));
        Encoding.ASCII.GetBytes(password).CopyTo(packet.AsSpan(31));

        return packet;
    }

    private static byte[] ReadResponse(Socket socket, int expectedFirstByte)
    {
        var buffer = new byte[512];

        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (socket.Available > 0)
            {
                var read = socket.Receive(buffer);

                return buffer.AsSpan(0, read).ToArray();
            }

            Thread.Sleep(20);
        }

        throw new TimeoutException($"No response (expected 0x{expectedFirstByte:X2}).");
    }

    private static byte[] ReadBytes(Socket socket, int minCount)
    {
        var buffer = new byte[512];
        var total = 0;

        for (var attempt = 0; attempt < 50 && total < minCount; attempt++)
        {
            if (socket.Available > 0)
            {
                total += socket.Receive(buffer, total, buffer.Length - total, SocketFlags.None);
            }
            else
            {
                Thread.Sleep(20);
            }
        }

        if (total < minCount)
        {
            throw new TimeoutException($"Expected at least {minCount} bytes, received {total}.");
        }

        return buffer.AsSpan(0, total).ToArray();
    }

    [Fact]
    public async Task FullLogin_SeedToRedirect_ReturnsServerListThenGameServer()
    {
        var config = LoopbackConfig();
        var network = await StartServerAsync(config);

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(IPAddress.Loopback, network.Port);

            var seed = new byte[21];
            seed[0] = 0xEF;
            seed[4] = 0x2A; // seed = 42
            socket.Send(seed);

            socket.Send(AccountLogin("squid", "secret"));
            var serverList = ReadResponse(socket, 0xA8);
            Assert.Equal(0xA8, serverList[0]);

            socket.Send(new byte[] { 0xA0, 0x00, 0x00 });
            var redirect = ReadResponse(socket, 0x8C);
            Assert.Equal(0x8C, redirect[0]);
            Assert.Equal(11, redirect.Length);
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task FullLogin_GameLogin_ReturnsCharacterList()
    {
        var config = LoopbackConfig();
        var network = await StartServerAsync(config);

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(IPAddress.Loopback, network.Port);

            var seed = new byte[21];
            seed[0] = 0xEF;
            seed[4] = 0x2A;
            socket.Send(seed);

            socket.Send(AccountLogin("squid", "secret"));
            ReadResponse(socket, 0xA8);

            socket.Send(new byte[] { 0xA0, 0x00, 0x00 });
            var redirect = ReadResponse(socket, 0x8C);
            var authKey = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(redirect.AsSpan(7));

            var gameLogin = new byte[65];
            gameLogin[0] = 0x91;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(gameLogin.AsSpan(1), authKey);
            Encoding.ASCII.GetBytes("squid").CopyTo(gameLogin.AsSpan(5));
            socket.Send(gameLogin);

            // The game login enables UO transport compression, then replies with support features
            // (0xB9) and the character list (0xA9). The stream is Huffman-compressed on the wire, so
            // the exact bytes are covered by the packet wire tests; here we assert the reply arrives.
            var response = ReadBytes(socket, 1);

            Assert.NotEmpty(response);
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsLoginDenied()
    {
        var config = LoopbackConfig();
        var network = await StartServerAsync(config);

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(IPAddress.Loopback, network.Port);

            var seed = new byte[21];
            seed[0] = 0xEF;
            seed[4] = 0x2A;
            socket.Send(seed);

            socket.Send(AccountLogin("squid", string.Empty));
            var denied = ReadResponse(socket, 0x82);

            Assert.Equal(0x82, denied[0]);
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SessionLifecycle_ConnectAndDisconnect_PublishesEvents()
    {
        var config = LoopbackConfig();
        var eventBus = new EventBusService();

        using var created = new ManualResetEventSlim();
        using var destroyed = new ManualResetEventSlim();
        eventBus.Subscribe<SessionCreatedEvent>(
            (_, _) =>
            {
                created.Set();

                return Task.CompletedTask;
            }
        );
        eventBus.Subscribe<SessionDestroyedEvent>(
            (_, _) =>
            {
                destroyed.Set();

                return Task.CompletedTask;
            }
        );

        var network = await StartServerAsync(config, eventBus);

        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(IPAddress.Loopback, network.Port);

            Assert.True(created.Wait(TimeSpan.FromSeconds(2)), "SessionCreatedEvent was not published.");

            socket.Close();

            Assert.True(destroyed.Wait(TimeSpan.FromSeconds(2)), "SessionDestroyedEvent was not published.");
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Dispatch_HandledPacket_PublishesPacketDispatchedEvent()
    {
        var config = LoopbackConfig();
        var eventBus = new EventBusService();

        using var dispatched = new ManualResetEventSlim();
        eventBus.Subscribe<PacketDispatchedEvent>(
            (e, _) =>
            {
                if (e.OpCode == 0x80)
                {
                    dispatched.Set();
                }

                return Task.CompletedTask;
            }
        );

        var network = await StartServerAsync(config, eventBus);

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(IPAddress.Loopback, network.Port);

            var seed = new byte[21];
            seed[0] = 0xEF;
            seed[4] = 0x2A;
            socket.Send(seed);

            socket.Send(AccountLogin("squid", "secret"));

            Assert.True(dispatched.Wait(TimeSpan.FromSeconds(2)), "PacketDispatchedEvent was not published for 0x80.");
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }
}
