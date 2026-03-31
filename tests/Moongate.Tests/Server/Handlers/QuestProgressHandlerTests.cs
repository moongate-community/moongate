using System.Net.Sockets;
using Moongate.Network.Client;
using Moongate.Server.Data.Events.Characters;
using Moongate.Server.Data.Events.Items;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Quests;
using Moongate.Tests.Server.Services.Spatial;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Templates.Quests;

namespace Moongate.Tests.Server.Handlers;

public sealed class QuestProgressHandlerTests
{
    private sealed class RecordingQuestService : IQuestService
    {
        public UOMobileEntity? LastKilledPlayer { get; private set; }

        public UOMobileEntity? LastKilledMobile { get; private set; }

        public int OnMobileKilledCallCount { get; private set; }

        public UOMobileEntity? LastInventoryPlayer { get; private set; }

        public int ReevaluateInventoryCallCount { get; private set; }

        public Task<bool> AcceptAsync(UOMobileEntity player, UOMobileEntity npc, string questId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<QuestTemplateDefinition>> GetAvailableForNpcAsync(UOMobileEntity player, UOMobileEntity npc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<QuestProgressEntity>> GetActiveForNpcAsync(UOMobileEntity player, UOMobileEntity npc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<QuestProgressEntity>> GetJournalAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task OnMobileKilledAsync(UOMobileEntity player, UOMobileEntity killedMobile, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            OnMobileKilledCallCount++;
            LastKilledPlayer = player;
            LastKilledMobile = killedMobile;

            return Task.CompletedTask;
        }

        public Task ReevaluateInventoryAsync(UOMobileEntity player, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            ReevaluateInventoryCallCount++;
            LastInventoryPlayer = player;

            return Task.CompletedTask;
        }

        public Task<bool> TryCompleteAsync(UOMobileEntity player, UOMobileEntity npc, string questId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    [Test]
    public async Task HandleAsync_MobileDeathEvent_PlayerKillerWithSessionCharacter_ShouldForwardKillToQuestService()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var killer = CreatePlayer((Serial)0x00001001u);
        var session = new GameSession(new(client))
        {
            CharacterId = killer.Id,
            Character = killer
        };
        var sessions = new FakeGameNetworkSessionService();
        sessions.Add(session);
        var questService = new RecordingQuestService();
        var handler = new QuestProgressHandler(sessions, questService);
        var victim = CreateNpc((Serial)0x00002001u);

        await handler.HandleAsync(new MobileDeathEvent(victim, killer, null, null));

        Assert.Multiple(() =>
        {
            Assert.That(questService.OnMobileKilledCallCount, Is.EqualTo(1));
            Assert.That(questService.LastKilledPlayer, Is.SameAs(session.Character));
            Assert.That(questService.LastKilledMobile, Is.SameAs(victim));
        });
    }

    [Test]
    public async Task HandleAsync_MobileDeathEvent_WhenKillerCannotBeResolved_ShouldNotCallQuestService()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var killer = CreatePlayer((Serial)0x00001002u);
        var session = new GameSession(new(client))
        {
            CharacterId = killer.Id,
            Character = null
        };
        var sessions = new FakeGameNetworkSessionService();
        sessions.Add(session);
        var questService = new RecordingQuestService();
        var handler = new QuestProgressHandler(sessions, questService);

        await handler.HandleAsync(new MobileDeathEvent(CreateNpc((Serial)0x00002002u), killer, null, null));

        Assert.That(questService.OnMobileKilledCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_ItemMovedEvent_PlayerSessionCharacter_ShouldForwardInventoryReevaluation()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = CreatePlayer((Serial)0x00001003u);
        var session = new GameSession(new(client))
        {
            CharacterId = player.Id,
            Character = player
        };
        var sessions = new FakeGameNetworkSessionService();
        sessions.Add(session);
        var questService = new RecordingQuestService();
        var handler = new QuestProgressHandler(sessions, questService);

        await handler.HandleAsync(
            new ItemMovedEvent(
                session.SessionId,
                (Serial)0x40001001u,
                Serial.Zero,
                (Serial)0x40002001u,
                new Point3D(1, 2, 3),
                new Point3D(4, 5, 6),
                0
            )
        );

        Assert.Multiple(() =>
        {
            Assert.That(questService.ReevaluateInventoryCallCount, Is.EqualTo(1));
            Assert.That(questService.LastInventoryPlayer, Is.SameAs(session.Character));
        });
    }

    [Test]
    public async Task HandleAsync_ItemMovedEvent_WhenSessionCharacterCannotBeResolved_ShouldNotCallQuestService()
    {
        using var client = new MoongateTCPClient(new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));
        var player = CreatePlayer((Serial)0x00001004u);
        var session = new GameSession(new(client))
        {
            CharacterId = player.Id,
            Character = null
        };
        var sessions = new FakeGameNetworkSessionService();
        sessions.Add(session);
        var questService = new RecordingQuestService();
        var handler = new QuestProgressHandler(sessions, questService);

        await handler.HandleAsync(
            new ItemMovedEvent(
                session.SessionId,
                (Serial)0x40001002u,
                Serial.Zero,
                (Serial)0x40002002u,
                new Point3D(1, 2, 3),
                new Point3D(4, 5, 6),
                0
            )
        );

        Assert.That(questService.ReevaluateInventoryCallCount, Is.EqualTo(0));
    }

    private static UOMobileEntity CreateNpc(Serial id)
        => new()
        {
            Id = id,
            IsPlayer = false,
            Name = "sewer_rat"
        };

    private static UOMobileEntity CreatePlayer(Serial id)
        => new()
        {
            Id = id,
            IsPlayer = true,
            Name = "player"
        };
}
