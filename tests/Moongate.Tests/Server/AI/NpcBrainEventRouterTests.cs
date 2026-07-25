using System.Runtime.CompilerServices;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Data.Internal.AI;
using Moongate.Server.Services.AI;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.UO.Data.Types;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Tests.Server.AI;

public class NpcBrainEventRouterTests
{
    [Fact]
    public async Task OnMobileSpeech_ActiveBrainsInExactHearingRange_ReceivePreservedSpeechSnapshot()
    {
        var fixture = new RouterFixture();
        var speaker = fixture.AddMobile(0x1, "Player One", 0, 10, 10);
        var shortRange = fixture.AddBrain(0x2, "Short", 0, 15, 10, 8, 5, true);
        var longRange = fixture.AddBrain(0x3, "Long", 0, 17, 10, 8, 7, true);
        fixture.AddBrain(0x4, "Outside Exact", 0, 16, 10, 8, 5, true);
        fixture.AddBrain(0x5, "Sleeping", 0, 11, 10, 8, 20, false);
        fixture.AddBrain(0x6, "Other Map", 1, 10, 10, 8, 20, true);
        fixture.Sessions.Played.Add(speaker.Id);

        await fixture.Router.OnMobileSpeech(
            new(speaker.Id, ChatMessageType.Yell, "Guards!"),
            CancellationToken.None
        );

        Assert.Equal([shortRange.Id, longRange.Id], fixture.Scheduler.Events.Select(entry => entry.MobileId));
        var routed = fixture.Scheduler.Events[0];
        Assert.Equal(NpcBrainHookType.SpeechHeard, routed.Hook);
        Assert.Equal(NpcBrainEventType.SpeechHeard, routed.Event.Type);
        Assert.Equal("Guards!", routed.Event.Text);
        Assert.Equal(ChatMessageType.Yell, routed.Event.SpeechType);
        Assert.Equal(speaker.Id, routed.Event.Mobile!.Id);
        Assert.Equal("Player One", routed.Event.Mobile.Name);
        Assert.True(routed.Event.Mobile.IsPlayer);
    }

    [Fact]
    public async Task OnMobileSpeech_LaterNpcReply_ExcludesEachSpeakerAndOnlyEnqueues()
    {
        var fixture = new RouterFixture();
        var player = fixture.AddMobile(0x1, "Player", 0, 10, 10);
        var firstNpc = fixture.AddBrain(0x2, "First", 0, 11, 10, 8, 8, true);
        var secondNpc = fixture.AddBrain(0x3, "Second", 0, 12, 10, 8, 8, true);

        await fixture.Router.OnMobileSpeech(
            new(player.Id, ChatMessageType.Regular, "hello"),
            CancellationToken.None
        );
        fixture.Scheduler.Events.Clear();

        await fixture.Router.OnMobileSpeech(
            new(firstNpc.Id, ChatMessageType.Regular, "greetings"),
            CancellationToken.None
        );

        var routed = Assert.Single(fixture.Scheduler.Events);
        Assert.Equal(secondNpc.Id, routed.MobileId);
        Assert.Equal(firstNpc.Id, routed.Event.Mobile!.Id);
        Assert.Equal("greetings", routed.Event.Text);
        Assert.Equal(0, fixture.Scheduler.TickCalls);
    }

    [Fact]
    public async Task OnMobileMoved_SubjectCrossesObserverRange_EmitsEnterMoveAndLeaveOnce()
    {
        var fixture = new RouterFixture();
        var observer = fixture.AddBrain(0x1, "Observer", 0, 10, 10, 5, 5, true);
        var subject = fixture.AddMobile(0x2, "Subject", 0, 20, 10);

        await fixture.MoveAsync(subject, 15, 10);
        await fixture.MoveAsync(subject, 13, 10);
        await fixture.MoveAsync(subject, 20, 10);

        Assert.Equal(
            [
                NpcBrainHookType.MobileEnteredRange,
                NpcBrainHookType.MobileMoved,
                NpcBrainHookType.MobileLeftRange
            ],
            fixture.Scheduler.Events.Select(entry => entry.Hook)
        );
        Assert.All(fixture.Scheduler.Events, entry => Assert.Equal(observer.Id, entry.MobileId));
        Assert.Equal(new Point3D(15, 10, 0), fixture.Scheduler.Events[0].Event.ToPosition);
        Assert.Equal(new Point3D(13, 10, 0), fixture.Scheduler.Events[1].Event.ToPosition);
        Assert.Equal(new Point3D(20, 10, 0), fixture.Scheduler.Events[2].Event.ToPosition);
    }

    [Fact]
    public async Task OnMobileMoved_RepeatedInsideMovement_MailboxRetainsLatestPosition()
    {
        var fixture = new RouterFixture();
        var observer = fixture.AddBrain(0x1, "Observer", 0, 10, 10, 5, 5, true);
        var subject = fixture.AddMobile(0x2, "Subject", 0, 11, 10);

        await fixture.MoveAsync(subject, 12, 10);
        await fixture.MoveAsync(subject, 13, 10);

        var queued = Assert.Single(fixture.Scheduler.CoalescedEventsFor(observer.Id));
        Assert.Equal(NpcBrainHookType.MobileMoved, queued.Hook);
        Assert.Equal(subject.Id, queued.Event.Mobile!.Id);
        Assert.Equal(new Point3D(12, 10, 0), queued.Event.FromPosition);
        Assert.Equal(new Point3D(13, 10, 0), queued.Event.ToPosition);
        Assert.Equal(new Point3D(13, 10, 0), queued.Event.Mobile.Position);
    }

    [Fact]
    public async Task OnMobileMoved_ActiveBrainObserver_RecomputesStationaryEntriesAndLeaves()
    {
        var fixture = new RouterFixture();
        fixture.Sectors.Active.Add((0, 0, 0));
        var observer = fixture.AddBrain(0x1, "Observer", 0, 5, 5, 4, 4, true);
        var leftBehind = fixture.AddMobile(0x2, "Left", 0, 4, 5);
        var newlySeen = fixture.AddMobile(0x3, "New", 0, 13, 5);
        await fixture.Router.OnSectorActivated(new(0, 0, 0), CancellationToken.None);
        fixture.Scheduler.Events.Clear();

        await fixture.MoveAsync(observer, 10, 5);

        Assert.Collection(
            fixture.Scheduler.Events,
            entry =>
            {
                Assert.Equal(observer.Id, entry.MobileId);
                Assert.Equal(NpcBrainHookType.MobileLeftRange, entry.Hook);
                Assert.Equal(leftBehind.Id, entry.Event.Mobile!.Id);
            },
            entry =>
            {
                Assert.Equal(observer.Id, entry.MobileId);
                Assert.Equal(NpcBrainHookType.MobileEnteredRange, entry.Hook);
                Assert.Equal(newlySeen.Id, entry.Event.Mobile!.Id);
            }
        );
    }

    [Fact]
    public async Task SectorLifecycle_ActivationSeedsAndSleepingObserverRetainsNoPerceptionSet()
    {
        var fixture = new RouterFixture();
        var observer = fixture.AddBrain(0x1, "Observer", 0, 5, 5, 5, 5, false);
        var subject = fixture.AddMobile(0x2, "Subject", 0, 8, 5);
        fixture.Sectors.Active.Add((0, 0, 0));

        await fixture.Router.OnSectorActivated(new(0, 0, 0), CancellationToken.None);

        var initial = Assert.Single(fixture.Scheduler.Events);
        Assert.Equal(observer.Id, initial.MobileId);
        Assert.Equal(NpcBrainHookType.MobileEnteredRange, initial.Hook);
        Assert.Equal(subject.Id, initial.Event.Mobile!.Id);

        fixture.Scheduler.Events.Clear();
        fixture.Sectors.Active.Remove((0, 0, 0));
        await fixture.Router.OnSectorDeactivated(new(0, 0, 0), CancellationToken.None);
        await fixture.MoveAsync(subject, 20, 5);
        await fixture.MoveAsync(subject, 8, 5);
        Assert.Empty(fixture.Scheduler.Events);

        fixture.Sectors.Active.Add((0, 0, 0));
        await fixture.Router.OnSectorActivated(new(0, 0, 0), CancellationToken.None);

        var reseeded = Assert.Single(fixture.Scheduler.Events);
        Assert.Equal(NpcBrainHookType.MobileEnteredRange, reseeded.Hook);
        Assert.Equal(subject.Id, reseeded.Event.Mobile!.Id);
    }

    [Fact]
    public async Task OnMobileMoved_BrainEntersInactiveSector_DeactivatesAndClearsObserverSet()
    {
        var fixture = new RouterFixture();
        fixture.Sectors.Active.Add((0, 0, 0));
        var observer = fixture.AddBrain(0x1, "Observer", 0, 5, 5, 5, 5, true);
        var subject = fixture.AddMobile(0x2, "Subject", 0, 8, 5);
        await fixture.Router.OnSectorActivated(new(0, 0, 0), CancellationToken.None);
        fixture.Scheduler.Events.Clear();

        await fixture.MoveAsync(observer, 32, 5);
        await fixture.MoveAsync(subject, 9, 5);

        Assert.Contains(observer.Id, fixture.Scheduler.DeactivateCalls);
        Assert.False(fixture.Scheduler.IsActive(observer.Id));
        Assert.DoesNotContain(fixture.Scheduler.Events, entry => entry.MobileId == observer.Id);

        fixture.Sectors.Active.Add((0, 2, 0));
        await fixture.Router.OnSectorActivated(new(0, 2, 0), CancellationToken.None);
        fixture.Scheduler.Events.Clear();
        await fixture.MoveAsync(subject, 33, 5);

        var enter = Assert.Single(fixture.Scheduler.Events, entry => entry.MobileId == observer.Id);
        Assert.Equal(NpcBrainHookType.MobileEnteredRange, enter.Hook);
    }

    [Fact]
    public async Task PlayerLifecycle_UsesEventSnapshotsBeforeSpatialSubscriberAndEmitsRangeTransitions()
    {
        var fixture = new RouterFixture();
        var observer = fixture.AddBrain(0x1, "Observer", 0, 10, 10, 5, 5, true);
        var player = fixture.Mobile(0x2, "Player", 0, 14, 10);

        await fixture.Router.OnPlayerEnteredWorld(
            new(7, new Serial(0x900), player),
            CancellationToken.None
        );

        var enter = Assert.Single(fixture.Scheduler.Events);
        Assert.Equal(observer.Id, enter.MobileId);
        Assert.Equal(NpcBrainHookType.MobileEnteredRange, enter.Hook);
        Assert.True(enter.Event.Mobile!.IsPlayer);
        fixture.Scheduler.Events.Clear();

        var session = SessionWithCharacter(player);
        await fixture.Router.OnSessionDestroyed(new(session), CancellationToken.None);

        var leave = Assert.Single(fixture.Scheduler.Events);
        Assert.Equal(observer.Id, leave.MobileId);
        Assert.Equal(NpcBrainHookType.MobileLeftRange, leave.Hook);
        Assert.Equal(player.Id, leave.Event.Mobile!.Id);
    }

    [Fact]
    public async Task MobileLifecycle_CreateRoutesBothRolesAndDeleteLeavesAndUnbinds()
    {
        var fixture = new RouterFixture();
        fixture.Sectors.Active.Add((0, 0, 0));
        var observer = fixture.AddBrain(0x1, "Observer", 0, 5, 5, 5, 5, true);
        await fixture.Router.OnSectorActivated(new(0, 0, 0), CancellationToken.None);
        fixture.Scheduler.Events.Clear();
        var created = fixture.Mobile(0x2, "Created Brain", 0, 8, 5, "guard");
        fixture.Scheduler.Descriptors[created.Id] = new("guard", 1000, 5, 5);

        await fixture.Router.OnMobileCreated(new(created), CancellationToken.None);

        Assert.Contains(created.Id, fixture.Scheduler.BindCalls);
        Assert.Collection(
            fixture.Scheduler.Events,
            entry =>
            {
                Assert.Equal(observer.Id, entry.MobileId);
                Assert.Equal(created.Id, entry.Event.Mobile!.Id);
            },
            entry =>
            {
                Assert.Equal(created.Id, entry.MobileId);
                Assert.Equal(observer.Id, entry.Event.Mobile!.Id);
            }
        );
        fixture.Scheduler.Events.Clear();

        await fixture.Router.OnMobileDeleted(new(created), CancellationToken.None);

        var leave = Assert.Single(fixture.Scheduler.Events);
        Assert.Equal(observer.Id, leave.MobileId);
        Assert.Equal(NpcBrainHookType.MobileLeftRange, leave.Hook);
        Assert.Contains(created.Id, fixture.Scheduler.UnbindCalls);
    }

    [Fact]
    public async Task CombatEvents_OnlyActiveAffectedBrainReceivesAttackerAndDamagePayloads()
    {
        var fixture = new RouterFixture();
        var active = fixture.AddBrain(0x1, "Active", 0, 10, 10, 5, 5, true);
        var sleeping = fixture.AddBrain(0x2, "Sleeping", 0, 10, 10, 5, 5, false);
        var attacker = fixture.AddMobile(0x3, "Attacker", 0, 11, 10);

        await fixture.Router.OnMobileAttacked(new(active.Id, attacker.Id), CancellationToken.None);
        await fixture.Router.OnMobileDamaged(new(active.Id, attacker.Id, 17), CancellationToken.None);
        await fixture.Router.OnMobileDied(new(active.Id, attacker.Id), CancellationToken.None);
        await fixture.Router.OnMobileAttacked(new(sleeping.Id, attacker.Id), CancellationToken.None);
        await fixture.Router.OnMobileDamaged(new(sleeping.Id, attacker.Id, 9), CancellationToken.None);
        await fixture.Router.OnMobileDied(new(sleeping.Id, attacker.Id), CancellationToken.None);

        Assert.Equal(
            [NpcBrainHookType.Attacked, NpcBrainHookType.Damage, NpcBrainHookType.Death],
            fixture.Scheduler.Events.Select(entry => entry.Hook)
        );
        Assert.All(fixture.Scheduler.Events, entry => Assert.Equal(active.Id, entry.MobileId));
        Assert.All(fixture.Scheduler.Events, entry => Assert.Equal(attacker.Id, entry.Event.Mobile!.Id));
        Assert.Equal(17, fixture.Scheduler.Events[1].Event.Amount);
    }

    [Fact]
    public async Task PerceptionTransitions_DifferentMapsNeverInteract()
    {
        var fixture = new RouterFixture();
        fixture.AddBrain(0x1, "Observer", 0, 10, 10, 20, 20, true);
        var subject = fixture.AddMobile(0x2, "Subject", 1, 10, 10);

        await fixture.MoveAsync(subject, 11, 10);
        await fixture.Router.OnMobileSpeech(
            new(subject.Id, ChatMessageType.Regular, "other map"),
            CancellationToken.None
        );

        Assert.Empty(fixture.Scheduler.Events);
    }

    [Fact]
    public void Subscribe_RegistersAllCanonicalEventHandlers()
    {
        var fixture = new RouterFixture();
        var eventBus = new RecordingSubscriptionEventBus();

        fixture.Router.Subscribe(eventBus);

        Assert.Equal(
            [
                typeof(MobileSpeechEvent),
                typeof(MobileMovedEvent),
                typeof(MobileCreatedEvent),
                typeof(MobileDeletedEvent),
                typeof(PlayerEnteredWorldEvent),
                typeof(SessionDestroyedEvent),
                typeof(SectorActivatedEvent),
                typeof(SectorDeactivatedEvent),
                typeof(MobileAttackedEvent),
                typeof(MobileDamagedEvent),
                typeof(MobileDiedEvent)
            ],
            eventBus.EventTypes
        );
    }

    private static PlayerSession SessionWithCharacter(MobileEntity character)
    {
        var session = (PlayerSession)RuntimeHelpers.GetUninitializedObject(typeof(PlayerSession));
        var property = typeof(PlayerSession).GetProperty(nameof(PlayerSession.Character)) ??
                       throw new InvalidOperationException("Player session character property was not found.");
        property.SetValue(session, character);

        return session;
    }

    private sealed class RouterFixture
    {
        public FakePersistenceService Persistence { get; } = new();

        public StubSessionManager Sessions { get; } = new();

        public RecordingScheduler Scheduler { get; } = new();

        public StubSectorActivityService Sectors { get; } = new();

        public SpatialIndexService Spatial { get; }

        public NpcBrainEventRouter Router { get; }

        public RouterFixture()
        {
            Spatial = new(Persistence, new StubLoopAffinity(), new StubEventBus());
            Router = new(Spatial, Persistence, Sessions, Scheduler, Sectors);
        }

        public MobileEntity AddBrain(
            uint serial,
            string name,
            int mapId,
            int x,
            int y,
            int perceptionRange,
            int hearingRange,
            bool active
        )
        {
            var mobile = AddMobile(serial, name, mapId, x, y, "guard");
            Scheduler.Descriptors[mobile.Id] = new("guard", 1000, perceptionRange, hearingRange);

            if (active)
            {
                Scheduler.Active.Add(mobile.Id);
            }

            return mobile;
        }

        public MobileEntity AddMobile(
            uint serial,
            string name,
            int mapId,
            int x,
            int y,
            string brainId = ""
        )
        {
            var mobile = Mobile(serial, name, mapId, x, y, brainId);
            Persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            Spatial.AddOrUpdate(mobile);

            return mobile;
        }

        public MobileEntity Mobile(
            uint serial,
            string name,
            int mapId,
            int x,
            int y,
            string brainId = ""
        )
            => new()
            {
                Id = new(serial),
                Name = name,
                BrainScriptId = brainId,
                MapId = mapId,
                Position = new(x, y, 0),
                Hits = 20,
                HitsMax = 30
            };

        public async Task MoveAsync(MobileEntity mobile, int x, int y, int? mapId = null)
        {
            var fromMapId = mobile.MapId;
            var fromPosition = mobile.Position;
            mobile.MapId = mapId ?? mobile.MapId;
            mobile.Position = new(x, y, mobile.Position.Z);
            await Persistence.Store<MobileEntity>().UpsertAsync(mobile);
            Spatial.AddOrUpdate(mobile);
            await Router.OnMobileMoved(
                new(mobile.Id, fromMapId, fromPosition, mobile.MapId, mobile.Position),
                CancellationToken.None
            );
        }
    }

    private sealed class RecordingScheduler : INpcBrainScheduler
    {
        public HashSet<Serial> Active { get; } = [];

        public Dictionary<Serial, BrainDescriptor> Descriptors { get; } = [];

        public List<Serial> BindCalls { get; } = [];

        public List<Serial> UnbindCalls { get; } = [];

        public List<Serial> ActivateCalls { get; } = [];

        public List<Serial> DeactivateCalls { get; } = [];

        public List<(Serial MobileId, NpcBrainHookType Hook, NpcBrainEvent Event)> Events { get; } = [];

        public int TickCalls { get; private set; }

        public int MaxHearingRange => Descriptors.Values.Select(value => value.HearingRange).DefaultIfEmpty().Max();

        public int MaxPerceptionRange => Descriptors.Values.Select(value => value.PerceptionRange).DefaultIfEmpty().Max();

        public void Bind(MobileEntity mobile)
        {
            BindCalls.Add(mobile.Id);
        }

        public void Unbind(Serial mobileId)
        {
            UnbindCalls.Add(mobileId);
            Active.Remove(mobileId);
            Descriptors.Remove(mobileId);
        }

        public void Activate(Serial mobileId)
        {
            ActivateCalls.Add(mobileId);

            if (Descriptors.ContainsKey(mobileId))
            {
                Active.Add(mobileId);
            }
        }

        public void Deactivate(Serial mobileId)
        {
            DeactivateCalls.Add(mobileId);
            Active.Remove(mobileId);
        }

        public void RefreshDescriptor(string brainId, BrainDescriptor descriptor)
        {
        }

        public bool IsActive(Serial mobileId)
            => Active.Contains(mobileId);

        public bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor)
        {
            var found = Descriptors.TryGetValue(mobileId, out var value);
            descriptor = value;

            return found;
        }

        public void EnqueueEvent(Serial mobileId, NpcBrainHookType hook, NpcBrainEvent brainEvent)
        {
            Events.Add((mobileId, hook, brainEvent));
        }

        public IReadOnlyList<(NpcBrainHookType Hook, NpcBrainEvent Event)> CoalescedEventsFor(
            Serial mobileId
        )
        {
            var mailbox = new NpcBrainMailbox(16, new NpcAiMetrics());

            foreach (var entry in Events.Where(entry => entry.MobileId == mobileId))
            {
                mailbox.Enqueue(entry.Hook, entry.Event);
            }

            return mailbox.Dequeue(16);
        }

        public void Tick()
        {
            TickCalls++;
        }
    }

    private sealed class StubSectorActivityService : ISectorActivityService
    {
        public HashSet<(int MapId, int SectorX, int SectorY)> Active { get; } = [];

        public SectorActivitySnapshot Current => new(Active.Count, 0);

        public bool IsActive(int mapId, int sectorX, int sectorY)
            => Active.Contains((mapId, sectorX, sectorY));

        public void TrackPlayer(MobileEntity player)
        {
        }

        public void MovePlayer(Serial playerId, int mapId, int sectorX, int sectorY)
        {
        }

        public void UntrackPlayer(Serial playerId)
        {
        }

        public void Tick()
        {
        }
    }

    private sealed class RecordingSubscriptionEventBus : IEventBus
    {
        public List<Type> EventTypes { get; } = [];

        public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        {
        }

        public Task PublishAsync<TEvent>(
            TEvent eventData,
            CancellationToken cancellationToken = default
        )
            where TEvent : IEvent
            => Task.CompletedTask;

        public IDisposable RegisterListener<TEvent>(IEventListener<TEvent> listener)
            where TEvent : IEvent
            => NoopDisposable.Instance;

        public IDisposable Subscribe<TEvent>(
            Func<TEvent, CancellationToken, Task> handler
        )
            where TEvent : IEvent
        {
            EventTypes.Add(typeof(TEvent));

            return NoopDisposable.Instance;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
