using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Handlers;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server;

public class LoginFlowIntegrationTests
{
    // How long a wire read waits before it is called a failure. Deliberately far above what the reads
    // actually take (milliseconds on an idle machine): the budget only has to outlast a loaded CI runner,
    // and nothing waits for it on the happy path, since every read returns the moment the bytes land. A
    // timeout here means the server never answered — not that it answered slowly.
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

    // Runs posted work inline (no game loop in the test), so inbound packets are processed
    // synchronously on the receive thread exactly as before the main-thread marshaling.
    private sealed class InlineDispatcher : IMainThreadDispatcher
    {
        public int PendingCount => 0;

        public int DrainPending(double? budgetMs = null)
            => 0;

        public void Post(Action action)
            => action();
    }

    [Fact]
    public async Task Dispatch_HandledPacket_PublishesPacketDispatchedEvent()
    {
        var config = LoopbackConfig();
        var eventBus = new EventBusService();

        using var dispatched = new ManualResetEventSlim();
        eventBus.Subscribe<PacketDispatchedEvent>((e, _) =>
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
            var authKey = BinaryPrimitives.ReadUInt32BigEndian(redirect.AsSpan(7));

            var gameLogin = new byte[65];
            gameLogin[0] = 0x91;
            BinaryPrimitives.WriteUInt32BigEndian(gameLogin.AsSpan(1), authKey);
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
    public async Task FullLogin_ThenCharacterCreation_PersistsTheCharacterWithItsGearAndEntersTheWorld()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();
        var accountId = (Serial)1;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "squid" });

        var eventBus = new EventBusService();
        using var enteredWorld = new ManualResetEventSlim();
        eventBus.Subscribe<PlayerEnteredWorldEvent>((_, _) =>
            {
                enteredWorld.Set();

                return Task.CompletedTask;
            }
        );

        var network = await StartServerAsync(config, eventBus, persistence);

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
            var authKey = BinaryPrimitives.ReadUInt32BigEndian(redirect.AsSpan(7));

            var gameLogin = new byte[65];
            gameLogin[0] = 0x91;
            BinaryPrimitives.WriteUInt32BigEndian(gameLogin.AsSpan(1), authKey);
            Encoding.ASCII.GetBytes("squid").CopyTo(gameLogin.AsSpan(5));
            socket.Send(gameLogin);
            ReadBytes(socket, 1); // features + character list

            socket.Send(CharacterCreation("Freydis"));

            // Entering the world is the last step of creation, so it proves the whole chain ran.
            Assert.True(enteredWorld.Wait(TimeSpan.FromSeconds(2)), "PlayerEnteredWorldEvent was not published.");

            // The character is persisted and linked to the account that created it.
            var mobile = Assert.Single(persistence.Store<MobileEntity>().Query());
            Assert.Equal("Freydis", mobile.Name);
            Assert.Contains(mobile.Id, persistence.Store<AccountEntity>().GetById(accountId)!.MobileIds);

            // Stats came through the packet and were validated (45 + 20 + 25 == 90), so the pools follow.
            Assert.Equal(45, mobile.Strength);
            Assert.Equal(72, mobile.HitsMax); // 50 + 45/2
            Assert.Equal(20, mobile.StaminaMax);
            Assert.Equal(25, mobile.ManaMax);

            // The chosen skill survived the trip, stored in tenths.
            Assert.Equal(500, mobile.Skills[1].Value);

            // Gear: backpack and bank box equipped, and the backpack is not empty.
            var items = new ItemService(persistence);
            Assert.NotEqual(Serial.Zero, mobile.BackpackId);
            Assert.Equal(mobile.BackpackId, mobile.EquippedItemIds[LayerType.Backpack]);
            Assert.True(mobile.EquippedItemIds.ContainsKey(LayerType.Bank));
            Assert.NotEmpty(items.GetContents(mobile.BackpackId));
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task FullLogin_EnterWorld_TooltipRequest_ReturnsMegaClilocOverTheWire()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = (Serial)1, Username = "squid" });

        var eventBus = new EventBusService();
        using var enteredWorld = new ManualResetEventSlim();
        eventBus.Subscribe<PlayerEnteredWorldEvent>((_, _) =>
            {
                enteredWorld.Set();

                return Task.CompletedTask;
            }
        );

        var network = await StartServerAsync(config, eventBus, persistence);

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
            var authKey = BinaryPrimitives.ReadUInt32BigEndian(redirect.AsSpan(7));

            var gameLogin = new byte[65];
            gameLogin[0] = 0x91;
            BinaryPrimitives.WriteUInt32BigEndian(gameLogin.AsSpan(1), authKey);
            Encoding.ASCII.GetBytes("squid").CopyTo(gameLogin.AsSpan(5));
            socket.Send(gameLogin);
            ReadBytes(socket, 1); // features + character list

            socket.Send(CharacterCreation("Freydis"));
            Assert.True(enteredWorld.Wait(TimeSpan.FromSeconds(2)), "PlayerEnteredWorldEvent was not published.");

            var mobile = Assert.Single(persistence.Store<MobileEntity>().Query());

            // Everything after game login travels huffman-compressed. Accumulate and re-decode the
            // stream while polling: a chunk may cut a code mid-packet.
            var compressed = new List<byte>();

            // The enter-world burst must have primed the tooltip revision (0xDC + serial).
            var oplInfoPattern = new byte[] { 0xDC }.Concat(SerialBytes(mobile.Id)).ToArray();
            Assert.True(
                PollUntil(socket, compressed, oplInfoPattern),
                "The enter-world burst carried no OplInfo (0xDC) for the player."
            );

            // Ask for the tooltip the way the client does: 0xD6 with one serial.
            var request = new byte[7];
            request[0] = 0xD6;
            BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(1), 7);
            SerialBytes(mobile.Id).CopyTo(request.AsSpan(3));
            socket.Send(request);

            // The response holds the 1050045 name line with "Freydis" in the UTF-16LE arguments.
            var nameArgs = Encoding.Unicode.GetBytes(" \tFreydis\t ");
            var megaClilocPattern = new byte[] { 0x00, 0x10, 0x05, 0xBD, 0x00, (byte)nameArgs.Length }
                .Concat(nameArgs)
                .ToArray();
            Assert.True(
                PollUntil(socket, compressed, megaClilocPattern),
                "No MegaCliloc (0xD6) response carrying the name line arrived."
            );

            // And it is framed as a 0xD6 for that serial (id, length, 0x0001, serial).
            var decoded = HuffmanDecoder.Decode(compressed.ToArray().AsSpan());
            var start = decoded.AsSpan().IndexOf(megaClilocPattern.AsSpan());
            var header = decoded.AsSpan(..start).LastIndexOf(new byte[] { 0xD6 }.AsSpan());
            Assert.True(header >= 0);
            Assert.Equal(0x0001, BinaryPrimitives.ReadUInt16BigEndian(decoded.AsSpan(header + 3)));
            Assert.Equal(mobile.Id.Value, BinaryPrimitives.ReadUInt32BigEndian(decoded.AsSpan(header + 5)));
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Keeps draining the socket into <paramref name="compressed" /> until the decoded stream contains <paramref name="pattern" />.</summary>
    private static bool PollUntil(Socket socket, List<byte> compressed, byte[] pattern)
    {
        var buffer = new byte[4096];
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            if (HuffmanDecoder.Decode(compressed.ToArray().AsSpan()).AsSpan().IndexOf(pattern.AsSpan()) >= 0)
            {
                return true;
            }

            var remaining = ReadTimeout - elapsed.Elapsed;

            if (remaining <= TimeSpan.Zero || !socket.Poll(remaining, SelectMode.SelectRead))
            {
                return false;
            }

            var read = socket.Receive(buffer);

            // A readable socket that yields nothing is a closed one: no further bytes are coming, so the
            // pattern this is waiting for never will either.
            if (read == 0)
            {
                return false;
            }

            compressed.AddRange(buffer.AsSpan(0, read).ToArray());
        }
    }

    private static byte[] SerialBytes(Serial serial)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, serial.Value);

        return bytes;
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
        eventBus.Subscribe<SessionCreatedEvent>((_, _) =>
            {
                created.Set();

                return Task.CompletedTask;
            }
        );
        eventBus.Subscribe<SessionDestroyedEvent>((_, _) =>
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

    private static byte[] AccountLogin(string account, string password)
    {
        var packet = new byte[62];
        packet[0] = 0x80;
        Encoding.ASCII.GetBytes(account).CopyTo(packet.AsSpan(1));
        Encoding.ASCII.GetBytes(password).CopyTo(packet.AsSpan(31));

        return packet;
    }

    /// <summary>
    /// The 106-byte creation packet (0xF8) for a male human with a valid 45/20/25 stat spread and one
    /// starting skill. Everything not asserted on is left zeroed, which also picks starting city 0.
    /// </summary>
    private static byte[] CharacterCreation(string name)
    {
        var packet = new byte[106];
        packet[0] = 0xF8;
        Encoding.ASCII.GetBytes(name).CopyTo(packet.AsSpan(10));

        packet[70] = 0;  // gender/race: male human
        packet[71] = 45; // strength
        packet[72] = 20; // dexterity
        packet[73] = 25; // intelligence, summing to the 90-point budget

        packet[74] = 1;  // first skill: Anatomy
        packet[75] = 50; // at 50, stored as 500 tenths

        return packet;
    }

    private static MoongateConfig LoopbackConfig()
        => new()
        {
            ShardName = "Moongate",
            Network = new() { Address = "127.0.0.1", Port = 0, PublicAddress = "127.0.0.1" }
        };

    private static byte[] ReadBytes(Socket socket, int minCount)
    {
        var buffer = new byte[512];
        var total = 0;
        var elapsed = Stopwatch.StartNew();

        while (total < minCount)
        {
            var remaining = ReadTimeout - elapsed.Elapsed;

            if (remaining <= TimeSpan.Zero || !socket.Poll(remaining, SelectMode.SelectRead))
            {
                break;
            }

            var read = socket.Receive(buffer, total, buffer.Length - total, SocketFlags.None);

            // A readable socket that yields nothing is a closed one: without this the loop would spin on
            // Poll until the deadline rather than report what it got.
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total < minCount)
        {
            throw new TimeoutException($"Expected at least {minCount} bytes, received {total}.");
        }

        return buffer.AsSpan(0, total).ToArray();
    }

    private static byte[] ReadResponse(Socket socket, int expectedFirstByte)
    {
        var buffer = new byte[512];

        if (!socket.Poll(ReadTimeout, SelectMode.SelectRead))
        {
            throw new TimeoutException($"No response (expected 0x{expectedFirstByte:X2}).");
        }

        var read = socket.Receive(buffer);

        if (read == 0)
        {
            throw new IOException($"Server closed the connection (expected 0x{expectedFirstByte:X2}).");
        }

        return buffer.AsSpan(0, read).ToArray();
    }

    private static Task<NetworkService> StartServerAsync(MoongateConfig config)
        => StartServerAsync(config, new EventBusService());

    private static Task<NetworkService> StartServerAsync(MoongateConfig config, IEventBus eventBus)
        => StartServerAsync(config, eventBus, new StubCharacterService(), null);

    /// <summary>Starts the server with a real character service over <paramref name="persistence" />.</summary>
    private static Task<NetworkService> StartServerAsync(
        MoongateConfig config,
        IEventBus eventBus,
        FakePersistenceService persistence
    )
    {
        var opl = new OplService(persistence, new ItemTemplateService());

        return StartServerAsync(
            config,
            eventBus,
            CharacterServiceFixture.Create(persistence, (EventBusService)eventBus),
            new WorldService(
                new ItemService(persistence, opl),
                CharacterServiceFixture.Skills(),
                new VirtualSerialService(),
                eventBus,
                TimeProvider.System,
                opl
            ),
            opl
        );
    }

    private static async Task<NetworkService> StartServerAsync(
        MoongateConfig config,
        IEventBus eventBus,
        ICharacterService characters,
        WorldService? world,
        OplService? opl = null
    )
    {
        var sessions = new SessionManager();
        var accounts = new StubAccountService();
        var pending = new PendingLoginStore(30000, () => Environment.TickCount64);

        var cities = new StartingCityService();
        cities.Register(
            new()
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

        var handlers = new List<IPacketHandlerRegistration>
        {
            new LoginSeedHandler(),
            new AccountLoginHandler(accounts, config),
            new SelectServerHandler(pending, config),
            new GameServerLoginHandler(pending, cities, accounts, characters)
        };

        if (world is not null)
        {
            handlers.Add(new CharacterCreationHandler(characters, world));
        }

        if (opl is not null)
        {
            handlers.Add(new MegaClilocHandler(opl));
        }

        var network = new NetworkService(sessions, config, [.. handlers], eventBus, new InlineDispatcher());
        await network.StartAsync(CancellationToken.None);

        return network;
    }
}
