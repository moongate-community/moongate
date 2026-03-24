using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Packets.Incoming.Login;
using Moongate.Network.Packets.Incoming.Movement;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Network.Packets.Outgoing.Login;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Network.Spans;
using Moongate.Server.Data.Packets;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.EvenLoop;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Handlers;

public sealed class CharacterHandlerTests
{
    private sealed class CharacterHandlerTestBackgroundJobService : IBackgroundJobService
    {
        private readonly Queue<Action> _pending = new();

        public void EnqueueBackground(Action job)
            => throw new NotSupportedException();

        public void EnqueueBackground(Func<Task> job)
            => throw new NotSupportedException();

        public int ExecutePendingOnGameLoop(int maxActions = 100)
        {
            var executed = 0;

            while (executed < maxActions && _pending.Count > 0)
            {
                _pending.Dequeue()();
                executed++;
            }

            return executed;
        }

        public void PostToGameLoop(Action action)
            => _pending.Enqueue(action);

        public void RunBackgroundAndPostResult<TResult>(
            Func<TResult> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void RunBackgroundAndPostResultAsync<TResult>(
            Func<Task<TResult>> backgroundJob,
            Action<TResult> onGameLoopResult,
            Action<Exception>? onGameLoopError = null
        )
            => throw new NotSupportedException();

        public void Start(int? workerCount = null)
            => throw new NotSupportedException();

        public Task StopAsync()
            => Task.CompletedTask;
    }

    [Test]
    public async Task HandleCharacterLoggedIn_ShouldDeferPlayerCharacterLoggedInEventUntilGameLoopCallback()
    {
        EnsureMapRegistered();

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new MovementHandlerTestCharacterService();
        var entityFactoryService = new CharacterHandlerTestEntityFactoryService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var backgroundJobs = new CharacterHandlerTestBackgroundJobService();
        var handler = new CharacterHandler(
            queue,
            characterService,
            entityFactoryService,
            eventBus,
            gameNetworkSessionService,
            new RegionDataLoaderTestSpatialWorldService(),
            backgroundJobService: backgroundJobs
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304,
            AccountType = AccountType.Regular
        };

        var characterId = (Serial)0x00000099;

        var result = await handler.HandleCharacterLoggedIn(session, characterId);
        Assert.That(eventBus.Events.OfType<PlayerCharacterLoggedInEvent>(), Is.Empty);

        var executedCallbacks = backgroundJobs.ExecutePendingOnGameLoop();
        var gameEvent = eventBus.Events.OfType<PlayerCharacterLoggedInEvent>().Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(executedCallbacks, Is.EqualTo(1));
                Assert.That(gameEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(gameEvent.AccountId, Is.EqualTo(session.AccountId));
                Assert.That(gameEvent.CharacterId, Is.EqualTo(characterId));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_CharacterCreation_ShouldApplyStarterEquipmentHuesFromPacket()
    {
        EnsureMapRegistered();

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new MovementHandlerTestCharacterService();
        var entityFactoryService = new CharacterHandlerTestEntityFactoryService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var backgroundJobs = new CharacterHandlerTestBackgroundJobService();
        var handler = new CharacterHandler(
            queue,
            characterService,
            entityFactoryService,
            eventBus,
            gameNetworkSessionService,
            new RegionDataLoaderTestSpatialWorldService(),
            backgroundJobService: backgroundJobs
        );

        var packet = new CharacterCreationPacket();
        var parsed = packet.TryParse(BuildCharacterCreationPayload());
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304,
            AccountType = AccountType.Regular
        };

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(handled, Is.True);
                Assert.That(characterService.ApplyStarterEquipmentHuesCalls, Is.EqualTo(1));
                Assert.That(characterService.LastAppliedCharacterId, Is.EqualTo((Serial)1u));
                Assert.That(characterService.LastAppliedShirtHue, Is.EqualTo(packet.Shirt.Hue));
                Assert.That(characterService.LastAppliedPantsHue, Is.EqualTo(packet.Pants.Hue));
            }
        );
    }

    [Test]
    public async Task HandlePacketAsync_RequestWarMode_ShouldUpdateCharacterAndEnqueueWarModePacket()
    {
        EnsureMapRegistered();

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new MovementHandlerTestCharacterService();
        var entityFactoryService = new CharacterHandlerTestEntityFactoryService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var backgroundJobs = new CharacterHandlerTestBackgroundJobService();
        var handler = new CharacterHandler(
            queue,
            characterService,
            entityFactoryService,
            eventBus,
            gameNetworkSessionService,
            new RegionDataLoaderTestSpatialWorldService(),
            backgroundJobService: backgroundJobs
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304,
            AccountType = AccountType.Regular,
            Character = new()
            {
                Id = (Serial)0x00000042,
                IsWarMode = false
            }
        };

        var packet = new RequestWarModePacket();
        var parsed = packet.TryParse(new byte[] { 0x72, 0x01, 0x00, 0x32, 0x00 });

        var handled = await handler.HandlePacketAsync(session, packet);

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed, Is.True);
                Assert.That(handled, Is.True);
                Assert.That(session.Character.IsWarMode, Is.True);
                Assert.That(queue.TryDequeue(out var queued), Is.True);
                Assert.That(queued.Packet, Is.TypeOf<WarModePacket>());
            }
        );
    }

    [Test]
    public async Task HandleCharacterLoggedIn_ShouldEnqueueMapChangeAndMapPatchesBeforeSupportFeatures()
    {
        EnsureMapRegistered();

        var queue = new BasePacketListenerTestOutgoingPacketQueue();
        var characterService = new MovementHandlerTestCharacterService();
        var entityFactoryService = new CharacterHandlerTestEntityFactoryService();
        var eventBus = new NetworkServiceTestGameEventBusService();
        var gameNetworkSessionService = new FakeGameNetworkSessionService();
        var backgroundJobs = new CharacterHandlerTestBackgroundJobService();
        var handler = new CharacterHandler(
            queue,
            characterService,
            entityFactoryService,
            eventBus,
            gameNetworkSessionService,
            new RegionDataLoaderTestSpatialWorldService(),
            backgroundJobService: backgroundJobs
        );

        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var session = new GameSession(new(client))
        {
            AccountId = (Serial)0x01020304,
            AccountType = AccountType.Regular
        };

        var result = await handler.HandleCharacterLoggedIn(session, (Serial)0x00000099);
        var packets = DequeuePackets(queue).Select(outgoing => outgoing.Packet).ToList();

        var loginConfirmIndex = packets.FindIndex(packet => packet is LoginConfirmPacket);
        var mapChangeIndex = packets.FindIndex(
            packet => packet is GeneralInformationPacket general &&
                      general.SubcommandType == GeneralInformationSubcommandType.SetCursorHueSetMap
        );
        var mapPatchesIndex = packets.FindIndex(
            packet => packet is GeneralInformationPacket general &&
                      general.SubcommandType == GeneralInformationSubcommandType.EnableMapDiff
        );
        var supportFeaturesIndex = packets.FindIndex(packet => packet is SupportFeaturesPacket);
        var drawPlayerIndex = packets.FindIndex(packet => packet is DrawPlayerPacket);

        Assert.Multiple(
            () =>
            {
                Assert.That(result, Is.True);
                Assert.That(loginConfirmIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(mapChangeIndex, Is.GreaterThan(loginConfirmIndex));
                Assert.That(mapPatchesIndex, Is.GreaterThan(mapChangeIndex));
                Assert.That(supportFeaturesIndex, Is.GreaterThan(mapPatchesIndex));
                Assert.That(drawPlayerIndex, Is.GreaterThan(supportFeaturesIndex));
            }
        );
    }

    private static byte[] BuildCharacterCreationPayload()
    {
        var writer = new SpanWriter(106, true);

        writer.Write((byte)0xF8);
        writer.Write(unchecked((int)0xEDEDEDED));
        writer.Write(unchecked((int)0xFFFFFFFF));
        writer.Write((byte)0x00);
        writer.WriteAscii("TestCharacter", 30);
        writer.Write((ushort)0);
        writer.Write((uint)ClientFlags.Trammel);
        writer.Write(0);
        writer.Write(0);
        writer.Write((byte)2);
        writer.Clear(15);
        writer.Write((byte)5);
        writer.Write((byte)60);
        writer.Write((byte)50);
        writer.Write((byte)40);
        writer.Write((byte)0);
        writer.Write((byte)50);
        writer.Write((byte)1);
        writer.Write((byte)50);
        writer.Write((byte)2);
        writer.Write((byte)50);
        writer.Write((byte)3);
        writer.Write((byte)50);
        writer.Write((short)0x0455);
        writer.Write((short)0x0203);
        writer.Write((short)0x0304);
        writer.Write((short)0x0506);
        writer.Write((short)0x0708);
        writer.Write((short)3);
        writer.Write((ushort)0);
        writer.Write((short)1);
        writer.Write(0);
        writer.Write((short)0x0888);
        writer.Write((short)0x0999);

        var payload = writer.ToArray();
        writer.Dispose();

        return payload;
    }

    private static void EnsureMapRegistered()
    {
        if (Map.GetMap(0) is null)
        {
            _ = Map.RegisterMap(
                0,
                0,
                0,
                6144,
                4096,
                SeasonType.Summer,
                "Felucca",
                MapRules.FeluccaRules
            );
        }
    }

    private static List<OutgoingGamePacket> DequeuePackets(BasePacketListenerTestOutgoingPacketQueue queue)
    {
        var packets = new List<OutgoingGamePacket>();

        while (queue.TryDequeue(out var outgoing))
        {
            packets.Add(outgoing);
        }

        return packets;
    }
}
