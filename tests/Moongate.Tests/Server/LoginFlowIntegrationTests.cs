using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using DryIoc;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Network.Packets.Outgoing;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Commands;
using Moongate.Server.Abstractions.Interfaces.Network;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Commands;
using Moongate.Server.Handlers;
using Moongate.Server.Services.Accounts;
using Moongate.Server.Services.Chat;
using Moongate.Server.Services.Commands;
using Moongate.Server.Services.Game;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Network;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.Ultima.Io;
using Moongate.Ultima.Maps;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Core.Interfaces.Threading;
using SquidStd.Core.Utils;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server;

[Collection("UltimaClientData")]
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
    public async Task Chat_SayBroadcastAndRateLimit_BehaveAsDesigned()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = (Serial)1, Username = "alice" });
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = (Serial)2, Username = "bob" });

        var eventBus = new EventBusService();
        var opl = new OplService(persistence, new ItemTemplateService());
        var sessions = new SessionManager();

        var world = new WorldService(
            new ItemService(persistence, opl),
            CharacterServiceFixture.Skills(),
            new VirtualSerialService(),
            eventBus,
            TimeProvider.System,
            opl,
            sessions
        );
        var chat = new ChatService(world, eventBus);

        var testCities = new StartingCityService();
        testCities.Register(
            new()
            {
                City = "TestTown",
                Building = "Origin",
                Description = 1075072,
                X = 5,
                Y = 5,
                Z = 0,
                Map = MapType.Trammel
            }
        );
        var characters = CharacterServiceFixture.Create(persistence, eventBus, sessions, testCities);

        using var aliceEntered = new ManualResetEventSlim();
        using var bobEntered = new ManualResetEventSlim();
        eventBus.Subscribe<PlayerEnteredWorldEvent>(
            (e, _) =>
            {
                if (e.Mobile.Name == "Alice")
                {
                    aliceEntered.Set();
                }
                else if (e.Mobile.Name == "Bob")
                {
                    bobEntered.Set();
                }

                return Task.CompletedTask;
            }
        );

        var network = await StartServerWithChatAsync(config, eventBus, characters, world, chat, opl, sessions);

        try
        {
            using var aliceSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnterWorld(aliceSocket, network.Port, "alice", "Alice");
            Assert.True(aliceEntered.Wait(TimeSpan.FromSeconds(2)), "Alice's PlayerEnteredWorldEvent was not published.");

            using var bobSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnterWorld(bobSocket, network.Port, "bob", "Bob");
            Assert.True(bobEntered.Wait(TimeSpan.FromSeconds(2)), "Bob's PlayerEnteredWorldEvent was not published.");

            var aliceMobile = persistence.Store<MobileEntity>().Query().Single(m => m.Name == "Alice");

            // Alice says "hi" (plain Regular speech, in range of Bob).
            var say = Speech("hi");
            aliceSocket.Send(say);

            var bobCompressed = new List<byte>();

            // Unlike the fixed-length 0x77 UpdatePlayerPacket the movement test matches on, 0xAE carries
            // a 2-byte length between the opcode and the serial, so matching on the serial's 4 bytes alone
            // (rather than hand-computing the length prefix) is what actually identifies this as Alice's
            // packet reaching Bob — nothing else in this test's packet traffic carries Alice's own serial
            // to Bob.
            Assert.True(
                PollUntil(bobSocket, bobCompressed, SerialBytes(aliceMobile.Id)),
                "Bob never received Alice's speech (0xAE)."
            );

            // Alice sends a "." command: no broadcast reaches Bob, but Alice gets a system reply
            // (sender serial 0x00000000) instead.
            await Task.Delay(50); // clear the 25ms chat rate limit from the first send
            var command = Speech(".kick Bob");
            aliceSocket.Send(command);

            var aliceCompressed = new List<byte>();

            // Matches on the reply text itself (big-endian unicode, via the same encoding
            // CommandService writes with) rather than hand-computing the packet's length-prefixed
            // byte offsets. ".kick" is not a registered command, so CommandService.Execute replies
            // with its generic "Unknown command." message — the same reply an unauthorized caller
            // of a real command would get, by design (see the command-system design doc).
            var systemPattern = Encoding.BigEndianUnicode.GetBytes("Unknown command");
            Assert.True(
                PollUntil(aliceSocket, aliceCompressed, systemPattern),
                "Alice never received the \"Unknown command.\" reply."
            );
            Assert.False(
                PollUntil(
                    bobSocket,
                    bobCompressed,
                    new byte[] { 0, (byte)'k', 0, (byte)'i', 0, (byte)'c' },
                    TimeSpan.FromMilliseconds(300)
                ),
                "Bob must not receive any broadcast for a \".\" command."
            );

            // Two sends back-to-back: the second is dropped by the 25ms rate limit, so Bob only ever
            // sees one occurrence of "spam" on the wire, not two.
            aliceSocket.Send(Speech("spam"));
            aliceSocket.Send(Speech("spam"));

            var spamPattern = new byte[] { 0, (byte)'s', 0, (byte)'p', 0, (byte)'a', 0, (byte)'m' };
            Assert.True(PollUntil(bobSocket, bobCompressed, spamPattern), "Bob never received the first \"spam\".");

            // Give the second (rate-limited) send time to arrive and be dropped server-side before
            // counting occurrences on the wire.
            await Task.Delay(100);

            var decoded = HuffmanDecoder.Decode(bobCompressed.ToArray().AsSpan());
            var occurrences = 0;
            var searchFrom = 0;

            while (true)
            {
                var index = decoded.AsSpan(searchFrom).IndexOf(spamPattern.AsSpan());

                if (index < 0)
                {
                    break;
                }

                occurrences++;
                searchFrom += index + spamPattern.Length;
            }

            Assert.Equal(1, occurrences); // the second "spam" was dropped by the 25ms rate limit
        }
        finally
        {
            await network.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Command_BroadcastAuthorizedAndUnauthorized_BehaveAsDesigned()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();
        await persistence.Store<AccountEntity>()
                         .UpsertAsync(
                             new()
                             {
                                 Id = (Serial)1,
                                 Username = "gm",
                                 PasswordHash = HashUtils.HashPassword("secret"),
                                 IsActive = true,
                                 AccountLevel = AccountLevelType.GrandMaster
                             }
                         );
        await persistence.Store<AccountEntity>()
                         .UpsertAsync(
                             new()
                             {
                                 Id = (Serial)2,
                                 Username = "player",
                                 PasswordHash = HashUtils.HashPassword("secret"),
                                 IsActive = true,
                                 AccountLevel = AccountLevelType.Player
                             }
                         );

        var eventBus = new EventBusService();
        var opl = new OplService(persistence, new ItemTemplateService());
        var sessions = new SessionManager();

        var world = new WorldService(
            new ItemService(persistence, opl),
            CharacterServiceFixture.Skills(),
            new VirtualSerialService(),
            eventBus,
            TimeProvider.System,
            opl,
            sessions
        );
        var chat = new ChatService(world, eventBus);

        var testCities = new StartingCityService();
        testCities.Register(
            new()
            {
                City = "TestTown",
                Building = "Origin",
                Description = 1075072,
                X = 5,
                Y = 5,
                Z = 0,
                Map = MapType.Trammel
            }
        );
        var characters = CharacterServiceFixture.Create(persistence, eventBus, sessions, testCities);
        var accounts = new AccountService(persistence, characters, sessions);
        // Mirrors the runtime path: register the command declaratively (name/level/help) instead of
        // scanning an attribute. The resolver is a throwaway container — the registration below closes
        // over an already-built instance, so it never actually resolves through it.
        var commands = new CommandService(
            [
                new CommandRegistration(
                    "broadcast|bc",
                    AccountLevelType.GrandMaster,
                    "Sends a server-wide system message.",
                    CommandSourceType.InGame,
                    _ => new BroadcastCommand(chat)
                )
            ],
            new Container(),
            accounts
        );

        using var gmEntered = new ManualResetEventSlim();
        using var playerEntered = new ManualResetEventSlim();
        eventBus.Subscribe<PlayerEnteredWorldEvent>(
            (e, _) =>
            {
                if (e.Mobile.Name == "GM")
                {
                    gmEntered.Set();
                }
                else if (e.Mobile.Name == "Player")
                {
                    playerEntered.Set();
                }

                return Task.CompletedTask;
            }
        );

        var network = await StartServerWithCommandsAsync(
                          config,
                          eventBus,
                          characters,
                          world,
                          chat,
                          commands,
                          accounts,
                          opl,
                          sessions
                      );

        try
        {
            using var gmSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnterWorld(gmSocket, network.Port, "gm", "GM");
            Assert.True(gmEntered.Wait(TimeSpan.FromSeconds(2)), "GM's PlayerEnteredWorldEvent was not published.");

            using var playerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnterWorld(playerSocket, network.Port, "player", "Player");
            Assert.True(
                playerEntered.Wait(TimeSpan.FromSeconds(2)),
                "Player's PlayerEnteredWorldEvent was not published."
            );

            // The GM (GrandMaster) broadcasts — both sessions receive it.
            gmSocket.Send(Speech(".broadcast Server restarting soon"));

            var gmCompressed = new List<byte>();
            var playerCompressed = new List<byte>();
            var broadcastPattern = Encoding.BigEndianUnicode.GetBytes("restarting soon");
            Assert.True(
                PollUntil(gmSocket, gmCompressed, broadcastPattern),
                "GM never received their own broadcast."
            );
            Assert.True(
                PollUntil(playerSocket, playerCompressed, broadcastPattern),
                "Player never received the GM's broadcast."
            );

            // The Player (below GrandMaster) tries the same command — no broadcast reaches anyone,
            // and the Player gets the generic "Unknown command." reply instead.
            await Task.Delay(50); // clear the 25ms chat rate limit from the first send
            playerSocket.Send(Speech(".broadcast should not work"));

            var unknownPattern = Encoding.BigEndianUnicode.GetBytes("Unknown command");
            Assert.True(
                PollUntil(playerSocket, playerCompressed, unknownPattern),
                "Player never received the \"Unknown command.\" reply for an unauthorized broadcast."
            );
            Assert.False(
                PollUntil(
                    gmSocket,
                    gmCompressed,
                    Encoding.BigEndianUnicode.GetBytes("should not work"),
                    TimeSpan.FromMilliseconds(300)
                ),
                "GM must not receive a broadcast triggered by an unauthorized Player."
            );
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

    [Fact]
    public async Task FullLogin_EnterWorld_TooltipRequest_ReturnsMegaClilocOverTheWire()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = (Serial)1, Username = "squid" });

        var eventBus = new EventBusService();
        using var enteredWorld = new ManualResetEventSlim();
        eventBus.Subscribe<PlayerEnteredWorldEvent>(
            (_, _) =>
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
    public async Task FullLogin_ThenCharacterCreation_PersistsTheCharacterWithItsGearAndEntersTheWorld()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();
        var accountId = (Serial)1;
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = accountId, Username = "squid" });

        var eventBus = new EventBusService();
        using var enteredWorld = new ManualResetEventSlim();
        eventBus.Subscribe<PlayerEnteredWorldEvent>(
            (_, _) =>
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
    public async Task Movement_TurnThenStep_AcksBothAndBroadcastsToNearbyPlayer()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = (Serial)1, Username = "alice" });

        var eventBus = new EventBusService();
        var opl = new OplService(persistence, new ItemTemplateService());
        var sessions = new SessionManager();

        // Synthetic 8x8-tile flat map at mapId 0 (Felucca). "testCities" below is registered as
        // starting city 0 for the character factory, so a freshly created character spawns at (5,5)
        // on this same tiny map — real client coordinates (e.g. Britain at 1495,1629, the shared
        // CharacterServiceFixture default) would fall outside it.
        var tileData = UltimaFixtures.BuildTileData();
        var mapBlock = UltimaFixtures.BuildMapBlock(3, 0);
        var dir = UltimaFixtures.CreateClientDirectory(("map0.mul", mapBlock), ("tiledata.mul", tileData));

        try
        {
            Files.SetDirectory(dir);
            TileData.Initialize();
            var map = new Map(dir, 0, 0, 8, 8);
            var mapProvider = new StubMapProvider(MapType.Felucca, map);

            var loopThread = new LoopThreadMarker();
            loopThread.Capture();
            var spatial = new SpatialIndexService(persistence, loopThread);
            new SpatialSubscriber(spatial, persistence).Subscribe(eventBus);

            var mapTiles = new MapTileService(mapProvider);
            var regions = new RegionService();

            var world = new WorldService(
                new ItemService(persistence, opl),
                CharacterServiceFixture.Skills(),
                new VirtualSerialService(),
                eventBus,
                TimeProvider.System,
                opl,
                sessions
            );
            var movement = new MovementService(mapTiles, regions, spatial, world, persistence, TimeProvider.System);

            // CharacterServiceFixture.Create wires its own MobileFactoryService, whose starting-city
            // lookup is independent of the city StartServerWithMovementAsync registers for the
            // client-facing character list — the fixture's default (Britain, off this tiny map) has to
            // be overridden here too, or a fresh character would spawn outside the synthetic map.
            var testCities = new StartingCityService();
            testCities.Register(
                new()
                {
                    City = "TestTown",
                    Building = "Origin",
                    Description = 1075072,
                    X = 5,
                    Y = 5,
                    Z = 0,
                    Map = MapType.Felucca
                }
            );
            var characters = CharacterServiceFixture.Create(persistence, eventBus, sessions, testCities);

            using var aliceEntered = new ManualResetEventSlim();
            using var bobEntered = new ManualResetEventSlim();
            eventBus.Subscribe<PlayerEnteredWorldEvent>(
                (e, _) =>
                {
                    if (e.Mobile.Name == "Alice")
                    {
                        aliceEntered.Set();
                    }
                    else if (e.Mobile.Name == "Bob")
                    {
                        bobEntered.Set();
                    }

                    return Task.CompletedTask;
                }
            );

            var network = await StartServerWithMovementAsync(config, eventBus, characters, world, movement, opl, sessions);

            try
            {
                using var aliceSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                EnterWorld(aliceSocket, network.Port, "alice", "Alice");
                Assert.True(
                    aliceEntered.Wait(TimeSpan.FromSeconds(2)),
                    "Alice's PlayerEnteredWorldEvent was not published."
                );

                using var bobSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                EnterWorld(bobSocket, network.Port, "bob", "Bob");
                Assert.True(bobEntered.Wait(TimeSpan.FromSeconds(2)), "Bob's PlayerEnteredWorldEvent was not published.");

                var aliceMobile = persistence.Store<MobileEntity>().Query().Single(m => m.Name == "Alice");

                // InMemoryEntityStore.Query() hands back a live reference, not a clone (unlike the real
                // store), so aliceMobile keeps aging as MovementService mutates it in place. Position is
                // an immutable struct, so copying it now freezes the pre-move value for the assertions
                // below instead of comparing the post-move mobile against itself.
                var aliceStartPosition = aliceMobile.Position;

                // Alice spawns facing North (the factory default); the first move request toward East
                // only turns her — real UO client behavior — and must still be acked.
                var turn = new byte[7];
                turn[0] = 0x02;
                turn[1] = 0x02; // DirectionType.East
                turn[2] = 0x00; // sequence
                aliceSocket.Send(turn);

                var aliceCompressed = new List<byte>();
                var turnAck = new byte[] { 0x22, 0x00, 0x01 }; // 0x22 + sequence 0 + Notoriety.Innocent
                Assert.True(PollUntil(aliceSocket, aliceCompressed, turnAck), "Alice never received an ack for the turn.");

                // A turn is an accepted move too, so it re-baselines the walk-rate-limit clock
                // (PlayerSession.LastMoveAt) exactly like a real step does. Clearing the 400ms walk
                // interval here is what makes the next request a real step rather than a rejected one —
                // without it, the two sends race the clock and the outcome depends on machine speed.
                await Task.Delay(450);

                // Second request, now already facing East, is a real step.
                var step = new byte[7];
                step[0] = 0x02;
                step[1] = 0x02; // DirectionType.East
                step[2] = 0x01; // sequence
                aliceSocket.Send(step);

                var stepAck = new byte[] { 0x22, 0x01, 0x01 }; // 0x22 + sequence 1 + Notoriety.Innocent
                Assert.True(PollUntil(aliceSocket, aliceCompressed, stepAck), "Alice never received an ack for the step.");

                var bobCompressed = new List<byte>();
                var broadcastPattern = new byte[] { 0x77 }.Concat(SerialBytes(aliceMobile.Id)).ToArray();
                Assert.True(
                    PollUntil(bobSocket, bobCompressed, broadcastPattern),
                    "Bob never received Alice's movement broadcast (0x77)."
                );

                var moved = persistence.Store<MobileEntity>().GetById(aliceMobile.Id)!;
                Assert.Equal(aliceStartPosition.X + 1, moved.Position.X);
                Assert.Equal(aliceStartPosition.Y, moved.Position.Y);
                Assert.Equal(DirectionType.East, moved.Direction);

                // The spatial index was re-indexed at the new position, closing the loop with the
                // earlier spatial-index feature. Range 0 (exact tile) rather than 1: Bob spawned at the
                // same city as Alice (the fixture registers only one starting city), so a range wide
                // enough to reach Alice's old tile would also still catch Bob standing right next to her.
                Assert.Single(spatial.GetMobilesInRange(0, moved.Position, 0));
            }
            finally
            {
                await network.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            Directory.Delete(dir, true);
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

    /// <summary>
    /// End-to-end proof that the spatial index and its consumers work together: two real characters log
    /// in and enter the world (same default starting city, so they land on the same tile), the
    /// WorldReadyEvent-independent PlayerEnteredWorldEvent path indexes both through
    /// <see cref="SpatialSubscriber" />, and <see cref="WorldService.SendToPlayersInRange{TPacket}" />
    /// delivers real bytes over the wire to the in-range session while skipping the excluded one.
    /// </summary>
    [Fact]
    public async Task Spatial_TwoPlayersEnterWorld_IndexesBothAndBroadcastSkipsExcludedSession()
    {
        var config = LoopbackConfig();
        var persistence = new FakePersistenceService();

        // StubAccountService.GetAccountIdByUsername always resolves to Serial(1) regardless of the
        // username on the wire, so both logins land on this one seeded account. That is fine here: the
        // spatial assertions below key off the created mobiles, not their owning account.
        await persistence.Store<AccountEntity>().UpsertAsync(new() { Id = (Serial)1, Username = "alice" });

        var eventBus = new EventBusService();
        var opl = new OplService(persistence, new ItemTemplateService());
        var sessions = new SessionManager();

        var loopThread = new LoopThreadMarker();
        loopThread.Capture();
        var spatial = new SpatialIndexService(persistence, loopThread);
        new SpatialSubscriber(spatial, persistence).Subscribe(eventBus);

        var world = new WorldService(
            new ItemService(persistence, opl),
            CharacterServiceFixture.Skills(),
            new VirtualSerialService(),
            eventBus,
            TimeProvider.System,
            opl,
            sessions
        );
        var characters = CharacterServiceFixture.Create(persistence, eventBus, sessions);

        using var aliceEntered = new ManualResetEventSlim();
        using var bobEntered = new ManualResetEventSlim();
        eventBus.Subscribe<PlayerEnteredWorldEvent>(
            (e, _) =>
            {
                if (e.Mobile.Name == "Alice")
                {
                    aliceEntered.Set();
                }
                else if (e.Mobile.Name == "Bob")
                {
                    bobEntered.Set();
                }

                return Task.CompletedTask;
            }
        );

        // WorldService and NetworkService share one SessionManager here, unlike the other harness
        // overloads: this is what lets SendToPlayersInRange below reach the actual connected sockets.
        var network = await StartServerAsync(config, eventBus, characters, world, opl, sessions);

        try
        {
            using var aliceSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnterWorld(aliceSocket, network.Port, "alice", "Alice");
            Assert.True(aliceEntered.Wait(TimeSpan.FromSeconds(2)), "Alice's PlayerEnteredWorldEvent was not published.");

            using var bobSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EnterWorld(bobSocket, network.Port, "bob", "Bob");
            Assert.True(bobEntered.Wait(TimeSpan.FromSeconds(2)), "Bob's PlayerEnteredWorldEvent was not published.");

            var aliceMobile = persistence.Store<MobileEntity>().Query().Single(m => m.Name == "Alice");
            var bobMobile = persistence.Store<MobileEntity>().Query().Single(m => m.Name == "Bob");

            // Same default starting city (index 0) for both: same map, same tile.
            Assert.Equal(aliceMobile.MapId, bobMobile.MapId);
            Assert.Equal(aliceMobile.Position, bobMobile.Position);

            // The bootstrap subscriber indexed both purely from PlayerEnteredWorldEvent — no WorldReadyEvent
            // fired in this harness, so this also proves the per-player path works standalone.
            Assert.True(
                WaitUntil(
                    () => spatial.GetMobilesInRange(aliceMobile.MapId, aliceMobile.Position, 18).Count == 2,
                    TimeSpan.FromSeconds(2)
                ),
                "The spatial index did not end up holding both players."
            );

            // A distinctive, single-use marker packet: opcode 0x4E (PersonalLightLevel) + Bob's serial +
            // level 77, which never appears elsewhere on the wire (Bob's own enter-world burst carries the
            // same opcode and serial but level 0).
            var marker = new PersonalLightLevelPacket(bobMobile.Id, 77);
            var pattern = new byte[] { 0x4E }.Concat(SerialBytes(bobMobile.Id)).Concat([(byte)77]).ToArray();

            var recipients = world.SendToPlayersInRange(
                aliceMobile.MapId,
                aliceMobile.Position,
                18,
                marker,
                aliceMobile.Id
            );

            Assert.Equal(1, recipients);

            var bobCompressed = new List<byte>();
            Assert.True(
                PollUntil(bobSocket, bobCompressed, pattern),
                "Bob (in range, not excluded) never received the broadcast."
            );

            var aliceCompressed = new List<byte>();
            Assert.False(
                PollUntil(aliceSocket, aliceCompressed, pattern, TimeSpan.FromMilliseconds(500)),
                "Alice (excluded) received a broadcast meant to skip her."
            );
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

    /// <summary>
    /// Drives one client through the full login handshake (seed, account login, server select, game
    /// login) and sends character creation for <paramref name="characterName" />, mirroring the inline
    /// steps every other test in this file repeats — parameterized so two independent clients can each
    /// reach the world over their own socket. Does not itself wait for the enter-world burst; callers
    /// observe that via <see cref="PlayerEnteredWorldEvent" /> or by reading the socket.
    /// </summary>
    private static void EnterWorld(Socket socket, int port, string account, string characterName)
    {
        socket.Connect(IPAddress.Loopback, port);

        var seed = new byte[21];
        seed[0] = 0xEF;
        seed[4] = 0x2A;
        socket.Send(seed);

        socket.Send(AccountLogin(account, "secret"));
        ReadResponse(socket, 0xA8);

        socket.Send(new byte[] { 0xA0, 0x00, 0x00 });
        var redirect = ReadResponse(socket, 0x8C);
        var authKey = BinaryPrimitives.ReadUInt32BigEndian(redirect.AsSpan(7));

        var gameLogin = new byte[65];
        gameLogin[0] = 0x91;
        BinaryPrimitives.WriteUInt32BigEndian(gameLogin.AsSpan(1), authKey);
        Encoding.ASCII.GetBytes(account).CopyTo(gameLogin.AsSpan(5));
        socket.Send(gameLogin);
        ReadBytes(socket, 1); // features + character list

        socket.Send(CharacterCreation(characterName));
    }

    private static MoongateConfig LoopbackConfig()
        => new()
        {
            ShardName = "Moongate",
            Network = new() { Address = "127.0.0.1", Port = 0, PublicAddress = "127.0.0.1" }
        };

    /// <summary>
    /// Keeps draining the socket into <paramref name="compressed" /> until the decoded stream contains
    /// <paramref name="pattern" />.
    /// </summary>
    private static bool PollUntil(Socket socket, List<byte> compressed, byte[] pattern)
        => PollUntil(socket, compressed, pattern, ReadTimeout);

    /// <summary>
    /// Same as the three-argument overload, but with an explicit <paramref name="timeout" /> — used to
    /// prove a pattern does NOT arrive, where waiting out the full <see cref="ReadTimeout" /> per assertion
    /// would make the test needlessly slow.
    /// </summary>
    private static bool PollUntil(Socket socket, List<byte> compressed, byte[] pattern, TimeSpan timeout)
    {
        var buffer = new byte[4096];
        var elapsed = Stopwatch.StartNew();

        while (true)
        {
            if (HuffmanDecoder.Decode(compressed.ToArray().AsSpan()).AsSpan().IndexOf(pattern.AsSpan()) >= 0)
            {
                return true;
            }

            var remaining = timeout - elapsed.Elapsed;

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

    private static byte[] SerialBytes(Serial serial)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, serial.Value);

        return bytes;
    }

    /// <summary>
    /// Builds a 0xAD Unicode Speech Request packet with Regular type, hue 0, font 3, language "ENU".
    /// The header is type(1) + hue(2) + font(2) + lang(4) = 9 bytes, plus id(1) + length(2) = 12 bytes
    /// before the text.
    /// </summary>
    private static byte[] Speech(string text)
    {
        var textBytes = Encoding.BigEndianUnicode.GetBytes(text + "\0");
        var packet = new byte[12 + textBytes.Length];
        packet[0] = 0xAD;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(1), (ushort)packet.Length);
        packet[3] = 0x00;                                           // ChatMessageType.Regular
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(4), 0); // hue
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(6), 3); // font
        packet[8] = (byte)'E';
        packet[9] = (byte)'N';
        packet[10] = (byte)'U';
        packet[11] = 0x00;
        textBytes.CopyTo(packet.AsSpan(12));

        return packet;
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
            new(
                new ItemService(persistence, opl),
                CharacterServiceFixture.Skills(),
                new VirtualSerialService(),
                eventBus,
                TimeProvider.System,
                opl,
                new SessionManager()
            ),
            opl
        );
    }

    private static async Task<NetworkService> StartServerAsync(
        MoongateConfig config,
        IEventBus eventBus,
        ICharacterService characters,
        WorldService? world,
        OplService? opl = null,
        ISessionManager? sessions = null
    )
    {
        sessions ??= new SessionManager();
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

    private static async Task<NetworkService> StartServerWithChatAsync(
        MoongateConfig config,
        IEventBus eventBus,
        ICharacterService characters,
        WorldService world,
        IChatService chat,
        OplService opl,
        ISessionManager sessions
    )
    {
        var accounts = new StubAccountService();
        var commands = new CommandService([], new Container(), accounts);
        var pending = new PendingLoginStore(30000, () => Environment.TickCount64);

        var cities = new StartingCityService();
        cities.Register(
            new()
            {
                City = "TestTown",
                Building = "Origin",
                Description = 1075072,
                X = 5,
                Y = 5,
                Z = 0,
                Map = MapType.Trammel
            }
        );

        var handlers = new List<IPacketHandlerRegistration>
        {
            new LoginSeedHandler(),
            new AccountLoginHandler(accounts, config),
            new SelectServerHandler(pending, config),
            new GameServerLoginHandler(pending, cities, accounts, characters),
            new CharacterCreationHandler(characters, world),
            new MegaClilocHandler(opl),
            new SpeechHandler(chat, commands)
        };

        var network = new NetworkService(sessions, config, [.. handlers], eventBus, new InlineDispatcher());
        await network.StartAsync(CancellationToken.None);

        return network;
    }

    private static async Task<NetworkService> StartServerWithCommandsAsync(
        MoongateConfig config,
        IEventBus eventBus,
        ICharacterService characters,
        WorldService world,
        IChatService chat,
        ICommandService commands,
        IAccountService accounts,
        OplService opl,
        ISessionManager sessions
    )
    {
        var pending = new PendingLoginStore(30000, () => Environment.TickCount64);

        var cities = new StartingCityService();
        cities.Register(
            new()
            {
                City = "TestTown",
                Building = "Origin",
                Description = 1075072,
                X = 5,
                Y = 5,
                Z = 0,
                Map = MapType.Trammel
            }
        );

        var handlers = new List<IPacketHandlerRegistration>
        {
            new LoginSeedHandler(),
            new AccountLoginHandler(accounts, config),
            new SelectServerHandler(pending, config),
            new GameServerLoginHandler(pending, cities, accounts, characters),
            new CharacterCreationHandler(characters, world),
            new MegaClilocHandler(opl),
            new SpeechHandler(chat, commands)
        };

        var network = new NetworkService(sessions, config, [.. handlers], eventBus, new InlineDispatcher());
        await network.StartAsync(CancellationToken.None);

        return network;
    }

    private static async Task<NetworkService> StartServerWithMovementAsync(
        MoongateConfig config,
        IEventBus eventBus,
        ICharacterService characters,
        WorldService world,
        IMovementService movement,
        OplService opl,
        ISessionManager sessions
    )
    {
        var accounts = new StubAccountService();
        var pending = new PendingLoginStore(30000, () => Environment.TickCount64);

        var cities = new StartingCityService();
        cities.Register(
            new()
            {
                City = "TestTown",
                Building = "Origin",
                Description = 1075072,
                X = 5,
                Y = 5,
                Z = 0,
                Map = MapType.Felucca
            }
        );

        var handlers = new List<IPacketHandlerRegistration>
        {
            new LoginSeedHandler(),
            new AccountLoginHandler(accounts, config),
            new SelectServerHandler(pending, config),
            new GameServerLoginHandler(pending, cities, accounts, characters),
            new CharacterCreationHandler(characters, world),
            new MegaClilocHandler(opl),
            new MoveRequestHandler(movement)
        };

        var network = new NetworkService(sessions, config, [.. handlers], eventBus, new InlineDispatcher());
        await network.StartAsync(CancellationToken.None);

        return network;
    }

    /// <summary>Polls <paramref name="condition" /> until it is true or <paramref name="timeout" /> elapses.</summary>
    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();

        while (!condition())
        {
            if (elapsed.Elapsed >= timeout)
            {
                return false;
            }

            Thread.Sleep(10);
        }

        return true;
    }
}
