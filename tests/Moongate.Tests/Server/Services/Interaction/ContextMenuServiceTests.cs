using System.Buffers.Binary;
using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Packets.Outgoing.Entity;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Events.Speech;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Services.Events;
using Moongate.Server.Services.Interaction;
using Moongate.Tests.Server.Support;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Quests;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.Services.Interaction;

public sealed class ContextMenuServiceTests
{
    private sealed class ContextMenuTestGameNetworkSessionService : IGameNetworkSessionService
    {
        private readonly Dictionary<long, GameSession> _sessions = [];

        public int Count => _sessions.Count;

        public void Add(GameSession session)
            => _sessions[session.SessionId] = session;

        public void Clear()
            => _sessions.Clear();

        public IReadOnlyCollection<GameSession> GetAll()
            => [.. _sessions.Values];

        public GameSession GetOrCreate(MoongateTCPClient client)
            => throw new NotSupportedException();

        public bool Remove(long sessionId)
            => _sessions.Remove(sessionId);

        public bool TryGet(long sessionId, out GameSession session)
            => _sessions.TryGetValue(sessionId, out session!);

        public bool TryGetByCharacterId(Serial characterId, out GameSession session)
        {
            foreach (var value in _sessions.Values)
            {
                if (value.CharacterId == characterId)
                {
                    session = value;

                    return true;
                }
            }

            session = null!;

            return false;
        }
    }

    private sealed class ContextMenuTestMobileService : IMobileService
    {
        public Dictionary<Serial, UOMobileEntity> MobilesById { get; } = [];

        public Task CreateOrUpdateAsync(UOMobileEntity mobile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> DeleteAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<UOMobileEntity?> GetAsync(Serial id, CancellationToken cancellationToken = default)
            => Task.FromResult(MobilesById.GetValueOrDefault(id));

        public Task<List<UOMobileEntity>> GetPersistentMobilesInSectorAsync(
            int mapId,
            int sectorX,
            int sectorY,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(new List<UOMobileEntity>());

        public Task<UOMobileEntity> SpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();

        public Task<(bool Spawned, UOMobileEntity? Mobile)> TrySpawnFromTemplateAsync(
            string templateId,
            Point3D location,
            int mapId,
            Serial? accountId = null,
            CancellationToken cancellationToken = default
        )
            => throw new NotSupportedException();
    }

    private sealed class TestVendorBuyRequestedEventListener : IGameEventListener<VendorBuyRequestedEvent>
    {
        public int Calls { get; private set; }

        public VendorBuyRequestedEvent LastEvent { get; private set; }

        public Task HandleAsync(VendorBuyRequestedEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            Calls++;
            LastEvent = gameEvent;

            return Task.CompletedTask;
        }
    }

    private sealed class TestVendorSellRequestedEventListener : IGameEventListener<VendorSellRequestedEvent>
    {
        public int Calls { get; private set; }

        public VendorSellRequestedEvent LastEvent { get; private set; }

        public Task HandleAsync(VendorSellRequestedEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            Calls++;
            LastEvent = gameEvent;

            return Task.CompletedTask;
        }
    }

    private sealed class TestQuestDialogRequestedEventListener : IGameEventListener<QuestDialogRequestedEvent>
    {
        public int Calls { get; private set; }

        public QuestDialogRequestedEvent LastEvent { get; private set; }

        public Task HandleAsync(QuestDialogRequestedEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            Calls++;
            LastEvent = gameEvent;

            return Task.CompletedTask;
        }
    }

    private sealed class ContextMenuTestQuestService : IQuestService
    {
        public IReadOnlyList<string> AvailableQuestIds { get; set; } = [];

        public IReadOnlyList<string> ActiveQuestIds { get; set; } = [];

        public Task<bool> AcceptAsync(UOMobileEntity player, UOMobileEntity npc, string questId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<QuestTemplateDefinition>> GetAvailableForNpcAsync(
            UOMobileEntity player,
            UOMobileEntity npc,
            CancellationToken cancellationToken = default
        )
        {
            _ = player;
            _ = cancellationToken;

            if (!npc.TryGetCustomString(MobileCustomParamKeys.Template.TemplateId, out var templateId) ||
                !string.Equals(templateId, "quest_giver_npc", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<QuestTemplateDefinition>>([]);
            }

            return Task.FromResult<IReadOnlyList<QuestTemplateDefinition>>(
                AvailableQuestIds.Select(
                    static questId =>
                        new QuestTemplateDefinition
                        {
                            Id = questId,
                            Name = "Quest Name",
                            Description = "Quest Description",
                            Category = "starter",
                            QuestGiverTemplateIds = ["quest_giver_npc"],
                            CompletionNpcTemplateIds = ["quest_giver_npc"],
                            MaxActivePerCharacter = 1
                        }
                ).ToList()
            );
        }

        public Task<IReadOnlyList<QuestProgressEntity>> GetActiveForNpcAsync(
            UOMobileEntity player,
            UOMobileEntity npc,
            CancellationToken cancellationToken = default
        )
        {
            _ = player;
            _ = cancellationToken;

            if (!npc.TryGetCustomString(MobileCustomParamKeys.Template.TemplateId, out var templateId) ||
                !string.Equals(templateId, "quest_turn_in_npc", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<IReadOnlyList<QuestProgressEntity>>([]);
            }

            return Task.FromResult<IReadOnlyList<QuestProgressEntity>>(
                ActiveQuestIds.Select(
                    static questId =>
                        new QuestProgressEntity
                        {
                            QuestId = questId,
                            Status = QuestProgressStatusType.ReadyToTurnIn
                        }
                ).ToList()
            );
        }

        public Task<IReadOnlyList<QuestProgressEntity>> GetJournalAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
        {
            _ = player;
            _ = cancellationToken;

            return Task.FromResult<IReadOnlyList<QuestProgressEntity>>([]);
        }

        public Task OnMobileKilledAsync(UOMobileEntity player, UOMobileEntity killedMobile, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReevaluateInventoryAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> TryCompleteAsync(UOMobileEntity player, UOMobileEntity npc, string questId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ContextMenuTestLuaBrainRunner : ILuaBrainRunner
    {
        public List<LuaBrainContextMenuEntry> Entries { get; } = [];

        public string? LastSelectedKey { get; private set; }

        public bool HandleSelectionResult { get; } = true;

        public void EnqueueCombatHook(Serial mobileId, LuaBrainCombatHookContext combatContext)
        {
            _ = mobileId;
            _ = combatContext;
        }

        public void EnqueueDeath(Serial mobileId, LuaBrainDeathContext deathContext)
        {
            _ = mobileId;
            _ = deathContext;
        }

        public void EnqueueInRange(Serial listenerNpcId, UOMobileEntity sourceMobile, int range = 3)
        {
            _ = listenerNpcId;
            _ = sourceMobile;
            _ = range;
        }

        public void EnqueueSpawn(MobileSpawnedFromSpawnerEvent gameEvent)
            => _ = gameEvent;

        public void EnqueueSpeech(SpeechHeardEvent gameEvent)
            => _ = gameEvent;

        public IReadOnlyList<LuaBrainContextMenuEntry> GetContextMenuEntries(
            UOMobileEntity mobile,
            UOMobileEntity? requester
        )
        {
            _ = mobile;
            _ = requester;

            return Entries;
        }

        public Task HandleAsync(SpeechHeardEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task HandleAsync(MobileAddedInWorldEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task HandleAsync(MobilePositionChangedEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public Task HandleAsync(MobileSpawnedFromSpawnerEvent gameEvent, CancellationToken cancellationToken = default)
        {
            _ = gameEvent;
            _ = cancellationToken;

            return Task.CompletedTask;
        }

        public void Register(UOMobileEntity mobile, string brainId)
        {
            _ = mobile;
            _ = brainId;
        }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public ValueTask TickAllAsync(long nowMilliseconds, CancellationToken cancellationToken = default)
        {
            _ = nowMilliseconds;
            _ = cancellationToken;

            return ValueTask.CompletedTask;
        }

        public bool TryHandleContextMenuSelection(
            UOMobileEntity mobile,
            UOMobileEntity? requester,
            string menuKey,
            long sessionId
        )
        {
            _ = mobile;
            _ = requester;
            _ = sessionId;
            LastSelectedKey = menuKey;

            return HandleSelectionResult;
        }

        public void Unregister(Serial mobileId)
            => _ = mobileId;
    }

    [Test]
    public async Task HandleAsync_ForContextMenuEntrySelectedEvent_ShouldEnqueuePaperdollPacket()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        session.Character = new()
        {
            Id = (Serial)0x00000001u,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        sessions.Add(session);

        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000002u,
            Name = "Guard",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        mobiles.MobilesById[target.Id] = target;

        await service.HandleAsync(new ContextMenuRequestedEvent(session.SessionId, target.Id));
        _ = outgoing.TryDequeue(out _); // context menu packet

        await service.HandleAsync(new ContextMenuEntrySelectedEvent(session.SessionId, target.Id, 1));

        var dequeued = outgoing.TryDequeue(out var outgoingPacket);

        Assert.That(dequeued, Is.True);
        Assert.That(outgoingPacket.Packet, Is.TypeOf<PaperdollPacket>());
    }

    [Test]
    public async Task HandleAsync_ForContextMenuEntrySelectedEvent_ShouldPublishVendorBuyRequestedEvent()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var listener = new TestVendorBuyRequestedEventListener();
        eventBus.RegisterListener(listener);
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        session.Character = new()
        {
            Id = (Serial)0x00000001u,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        sessions.Add(session);

        var vendor = new UOMobileEntity
        {
            Id = (Serial)0x0000002Au,
            Name = "Vendor",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        vendor.SetCustomString("sell_profile_id", "vendor.blacksmith");
        mobiles.MobilesById[vendor.Id] = vendor;

        await service.HandleAsync(new ContextMenuRequestedEvent(session.SessionId, vendor.Id));
        _ = outgoing.TryDequeue(out _);

        await service.HandleAsync(new ContextMenuEntrySelectedEvent(session.SessionId, vendor.Id, 2));

        Assert.Multiple(
            () =>
            {
                Assert.That(listener.Calls, Is.EqualTo(1));
                Assert.That(listener.LastEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(listener.LastEvent.VendorSerial, Is.EqualTo(vendor.Id));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForContextMenuEntrySelectedEvent_ShouldPublishVendorSellRequestedEvent()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var listener = new TestVendorSellRequestedEventListener();
        eventBus.RegisterListener(listener);
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        session.Character = new()
        {
            Id = (Serial)0x00000001u,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        sessions.Add(session);

        var vendor = new UOMobileEntity
        {
            Id = (Serial)0x0000002Bu,
            Name = "Vendor",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        vendor.SetCustomString("sell_profile_id", "vendor.blacksmith");
        mobiles.MobilesById[vendor.Id] = vendor;

        await service.HandleAsync(new ContextMenuRequestedEvent(session.SessionId, vendor.Id));
        _ = outgoing.TryDequeue(out _);

        await service.HandleAsync(new ContextMenuEntrySelectedEvent(session.SessionId, vendor.Id, 3));

        Assert.Multiple(
            () =>
            {
                Assert.That(listener.Calls, Is.EqualTo(1));
                Assert.That(listener.LastEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(listener.LastEvent.VendorSerial, Is.EqualTo(vendor.Id));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForContextMenuEntrySelectedEvent_WhenQuestEntryIsSelected_ShouldPublishQuestDialogRequestedEvent()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var questListener = new TestQuestDialogRequestedEventListener();
        eventBus.RegisterListener(questListener);
        var questService = new ContextMenuTestQuestService
        {
            AvailableQuestIds = ["starter.rat_hunt"]
        };
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus, questService: questService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        session.Character = new()
        {
            Id = (Serial)0x00000001u,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        sessions.Add(session);

        var npc = new UOMobileEntity
        {
            Id = (Serial)0x0000002Cu,
            Name = "Quest Giver",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        npc.SetCustomString(MobileCustomParamKeys.Template.TemplateId, "quest_giver_npc");
        mobiles.MobilesById[npc.Id] = npc;

        await service.HandleAsync(new ContextMenuRequestedEvent(session.SessionId, npc.Id));
        _ = outgoing.TryDequeue(out _);

        await service.HandleAsync(new ContextMenuEntrySelectedEvent(session.SessionId, npc.Id, 4));

        Assert.Multiple(
            () =>
            {
                Assert.That(questListener.Calls, Is.EqualTo(1));
                Assert.That(questListener.LastEvent.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(questListener.LastEvent.TargetSerial, Is.EqualTo(npc.Id));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForContextMenuRequestedEvent_WhenQuestNPCHasAvailableQuests_ShouldIncludeQuestEntry()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var questService = new ContextMenuTestQuestService
        {
            AvailableQuestIds = ["starter.rat_hunt"]
        };
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus, questService: questService);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        session.Character = new()
        {
            Id = (Serial)0x00000001u,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        sessions.Add(session);

        var npc = new UOMobileEntity
        {
            Id = (Serial)0x0000002Du,
            Name = "Quest Giver",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        npc.SetCustomString(MobileCustomParamKeys.Template.TemplateId, "quest_giver_npc");
        mobiles.MobilesById[npc.Id] = npc;

        var sent = await service.SendContextMenuAsync(session.SessionId, npc.Id);

        Assert.That(sent, Is.True);
        Assert.That(outgoing.TryDequeue(out var outgoingPacket), Is.True);
        Assert.That(outgoingPacket.Packet, Is.TypeOf<GeneralInformationPacket>());

        var packet = (GeneralInformationPacket)outgoingPacket.Packet;
        var payload = packet.SubcommandData.Span;
        Assert.That(payload[6], Is.EqualTo(2));

        var firstTag = BinaryPrimitives.ReadUInt16BigEndian(payload[7..9]);
        var secondTag = BinaryPrimitives.ReadUInt16BigEndian(payload[13..15]);

        Assert.Multiple(
            () =>
            {
                Assert.That(new[] { firstTag, secondTag }, Does.Contain((ushort)1));
                Assert.That(new[] { firstTag, secondTag }, Does.Contain((ushort)4));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForContextMenuRequestedEvent_ShouldEnqueueDisplayContextMenuPacket()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client));
        session.SetClientVersion(new("7.0.114.0"));
        session.Character = new()
        {
            Id = (Serial)0x00000001u,
            MapId = 0,
            Location = new(100, 100, 0)
        };
        sessions.Add(session);

        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000009u,
            Name = "Vendor",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        target.SetCustomString("sell_profile_id", "blacksmith_vendor");
        mobiles.MobilesById[target.Id] = target;

        await service.HandleAsync(new ContextMenuRequestedEvent(session.SessionId, target.Id));

        var dequeued = outgoing.TryDequeue(out var outgoingPacket);

        Assert.That(dequeued, Is.True);
        Assert.That(outgoingPacket.Packet, Is.TypeOf<GeneralInformationPacket>());
        var packet = (GeneralInformationPacket)outgoingPacket.Packet;
        Assert.That(packet.SubcommandType, Is.EqualTo(GeneralInformationSubcommandType.DisplayPopupContextMenu));

        var payload = packet.SubcommandData.ToArray();
        Assert.Multiple(
            () =>
            {
                Assert.That(payload[0], Is.EqualTo(0x00));
                Assert.That(payload[1], Is.EqualTo(0x01));
                Assert.That(BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(2, 4)), Is.EqualTo((uint)0x00000009));
                Assert.That(payload[6], Is.EqualTo(3));
            }
        );
    }

    [Test]
    public async Task HandleAsync_ForScriptContextMenuSelection_ShouldDispatchLuaSelection()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var luaBrainRunner = new ContextMenuTestLuaBrainRunner();
        luaBrainRunner.Entries.Add(new("feed", 3_006_135));
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus, luaBrainRunner);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000001u,
                MapId = 0,
                Location = new(100, 100, 0)
            }
        };
        session.SetClientVersion(new("7.0.114.0"));
        sessions.Add(session);

        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000009u,
            Name = "Orion",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        mobiles.MobilesById[target.Id] = target;

        await service.HandleAsync(new ContextMenuRequestedEvent(session.SessionId, target.Id));

        var dequeued = outgoing.TryDequeue(out var outgoingPacket);
        Assert.That(dequeued, Is.True);
        Assert.That(outgoingPacket.Packet, Is.TypeOf<GeneralInformationPacket>());
        var packet = (GeneralInformationPacket)outgoingPacket.Packet;
        var payload = packet.SubcommandData.Span;
        Assert.That(payload[6], Is.EqualTo(1));
        var customTag = BinaryPrimitives.ReadUInt16BigEndian(payload[7..9]);

        await service.HandleAsync(new ContextMenuEntrySelectedEvent(session.SessionId, target.Id, customTag));

        Assert.That(luaBrainRunner.LastSelectedKey, Is.EqualTo("feed"));
    }

    [Test]
    public async Task SendContextMenuAsync_WhenCustomEntriesContainInvalidValues_ShouldFilterAndKeepOrder()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var luaBrainRunner = new ContextMenuTestLuaBrainRunner();
        luaBrainRunner.Entries.Add(new("first", 3_006_135));
        luaBrainRunner.Entries.Add(new("", 3_006_135));
        luaBrainRunner.Entries.Add(new("invalid_cliloc", 2_999_999));
        luaBrainRunner.Entries.Add(new("second", 3_006_145));
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus, luaBrainRunner);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000001u,
                MapId = 0,
                Location = new(100, 100, 0)
            }
        };
        session.SetClientVersion(new("7.0.114.0"));
        sessions.Add(session);

        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000009u,
            Name = "Orion",
            MapId = 0,
            Location = new(101, 100, 0)
        };
        mobiles.MobilesById[target.Id] = target;

        var sent = await service.SendContextMenuAsync(session.SessionId, target.Id);

        Assert.That(sent, Is.True);
        Assert.That(outgoing.TryDequeue(out var outgoingPacket), Is.True);
        Assert.That(outgoingPacket.Packet, Is.TypeOf<GeneralInformationPacket>());

        var packet = (GeneralInformationPacket)outgoingPacket.Packet;
        var payload = packet.SubcommandData.Span;
        var entryCount = payload[6];

        // 2 valid script entries
        Assert.That(entryCount, Is.EqualTo(2));

        var tags = new List<ushort>(entryCount);
        var offset = 7;

        for (var index = 0; index < entryCount; index++)
        {
            var tag = BinaryPrimitives.ReadUInt16BigEndian(payload[offset..(offset + 2)]);
            var flags = BinaryPrimitives.ReadUInt16BigEndian(payload[(offset + 4)..(offset + 6)]);
            tags.Add(tag);
            offset += 6;

            if ((flags & 0x20) != 0)
            {
                offset += 2;
            }
        }

        var firstScriptTag = tags[0];
        var secondScriptTag = tags[1];

        await service.HandleAsync(new ContextMenuEntrySelectedEvent(session.SessionId, target.Id, firstScriptTag));
        Assert.That(luaBrainRunner.LastSelectedKey, Is.EqualTo("first"));

        // Context menu selection consumes pending state for the session: request it again.
        sent = await service.SendContextMenuAsync(session.SessionId, target.Id);
        Assert.That(sent, Is.True);
        Assert.That(outgoing.TryDequeue(out outgoingPacket), Is.True);
        Assert.That(outgoingPacket.Packet, Is.TypeOf<GeneralInformationPacket>());
        packet = (GeneralInformationPacket)outgoingPacket.Packet;
        payload = packet.SubcommandData.Span;
        entryCount = payload[6];
        tags.Clear();
        offset = 7;

        for (var index = 0; index < entryCount; index++)
        {
            var tag = BinaryPrimitives.ReadUInt16BigEndian(payload[offset..(offset + 2)]);
            var flags = BinaryPrimitives.ReadUInt16BigEndian(payload[(offset + 4)..(offset + 6)]);
            tags.Add(tag);
            offset += 6;

            if ((flags & 0x20) != 0)
            {
                offset += 2;
            }
        }

        secondScriptTag = tags[1];
        await service.HandleAsync(new ContextMenuEntrySelectedEvent(session.SessionId, target.Id, secondScriptTag));
        Assert.That(luaBrainRunner.LastSelectedKey, Is.EqualTo("second"));
    }

    [Test]
    public async Task SendContextMenuAsync_WhenGameMasterAndTargetOutOfRange_ShouldReturnTrue()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            AccountType = AccountType.GameMaster,
            Character = new()
            {
                Id = (Serial)0x00000001u,
                MapId = 0,
                Location = new(100, 100, 0)
            }
        };
        session.SetClientVersion(new("7.0.114.0"));
        sessions.Add(session);

        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000031u,
            Name = "Far",
            MapId = 0,
            Location = new(200, 200, 0)
        };
        target.SetCustomString("sell_profile_id", "vendor.blacksmith");
        mobiles.MobilesById[target.Id] = target;

        var result = await service.SendContextMenuAsync(session.SessionId, target.Id);

        Assert.That(result, Is.True);
        Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(1));
    }

    [Test]
    public async Task SendContextMenuAsync_WhenNpcHasCustomEntries_ShouldNotIncludePaperdollEntry()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var luaBrainRunner = new ContextMenuTestLuaBrainRunner();
        luaBrainRunner.Entries.Add(new("give_food", 3_006_135));
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus, luaBrainRunner);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            Character = new()
            {
                Id = (Serial)0x00000001u,
                MapId = 0,
                Location = new(100, 100, 0)
            }
        };
        session.SetClientVersion(new("7.0.114.0"));
        sessions.Add(session);

        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000009u,
            Name = "Orion",
            IsPlayer = false,
            MapId = 0,
            Location = new(101, 100, 0)
        };
        mobiles.MobilesById[target.Id] = target;

        var sent = await service.SendContextMenuAsync(session.SessionId, target.Id);

        Assert.That(sent, Is.True);
        Assert.That(outgoing.TryDequeue(out var outgoingPacket), Is.True);
        Assert.That(outgoingPacket.Packet, Is.TypeOf<GeneralInformationPacket>());

        var packet = (GeneralInformationPacket)outgoingPacket.Packet;
        var payload = packet.SubcommandData.Span;
        Assert.That(payload[6], Is.EqualTo(1));

        var firstTag = BinaryPrimitives.ReadUInt16BigEndian(payload[7..9]);
        var firstClilocOffset = BinaryPrimitives.ReadUInt16BigEndian(payload[9..11]);
        Assert.That(firstTag, Is.EqualTo(1000));
        Assert.That(firstClilocOffset, Is.EqualTo(6135));
    }

    [Test]
    public async Task SendContextMenuAsync_WhenRegularAndTargetOutOfRange_ShouldReturnFalse()
    {
        var sessions = new ContextMenuTestGameNetworkSessionService();
        var mobiles = new ContextMenuTestMobileService();
        var outgoing = new BasePacketListenerTestOutgoingPacketQueue();
        var eventBus = new GameEventBusService();
        var service = new ContextMenuService(sessions, mobiles, outgoing, eventBus);
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

        var session = new GameSession(new(client))
        {
            AccountType = AccountType.Regular,
            Character = new()
            {
                Id = (Serial)0x00000001u,
                MapId = 0,
                Location = new(100, 100, 0)
            }
        };
        session.SetClientVersion(new("7.0.114.0"));
        sessions.Add(session);

        var target = new UOMobileEntity
        {
            Id = (Serial)0x00000030u,
            Name = "Far",
            MapId = 0,
            Location = new(200, 200, 0)
        };
        target.SetCustomString("sell_profile_id", "vendor.blacksmith");
        mobiles.MobilesById[target.Id] = target;

        var result = await service.SendContextMenuAsync(session.SessionId, target.Id);

        Assert.That(result, Is.False);
        Assert.That(outgoing.CurrentQueueDepth, Is.EqualTo(0));
    }
}
