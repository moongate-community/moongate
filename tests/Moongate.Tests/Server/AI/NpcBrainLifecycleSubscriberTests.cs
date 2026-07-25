using Moongate.Core.Primitives;
using Moongate.Core.Extensions;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Tests.Server.AI;

public sealed class NpcBrainLifecycleSubscriberTests
{
    [Fact]
    public async Task OnWorldReady_AccountOwnedMobileWithBrainId_BindsOnlyNpcBrains()
    {
        var fixture = new LifecycleFixture();
        var npc = fixture.AddMobile(0x1, "guard");
        var player = fixture.AddMobile(0x2, "guard");
        await fixture.Persistence.Store<AccountEntity>()
            .UpsertAsync(new() { Id = new(0x100), Username = "player", MobileIds = [player.Id] });

        await fixture.Subscriber.OnWorldReady(new(), CancellationToken.None);

        Assert.Equal([npc.Id], fixture.Scheduler.Bound);
        Assert.Empty(fixture.Scheduler.Active);
    }

    [Fact]
    public async Task OnWorldReady_ActiveNpcSector_BindsAndActivatesBrain()
    {
        var fixture = new LifecycleFixture();
        var npc = fixture.AddMobile(0x1, "guard", x: 32, y: 48);
        fixture.Sectors.Active.Add((0, 2, 3));

        await fixture.Subscriber.OnWorldReady(new(), CancellationToken.None);

        Assert.Equal([npc.Id], fixture.Scheduler.Bound);
        Assert.Equal([npc.Id], fixture.Scheduler.Activated);
    }

    [Fact]
    public async Task OnMobileCreated_ActiveSector_BindsAndActivatesBrain()
    {
        var fixture = new LifecycleFixture();
        var mobile = fixture.AddMobile(0x1, "guard", x: 32, y: 48);
        fixture.Sectors.Active.Add((0, 2, 3));

        await fixture.Subscriber.OnMobileCreated(new(mobile), CancellationToken.None);

        Assert.Equal([mobile.Id], fixture.Scheduler.Bound);
        Assert.Equal([mobile.Id], fixture.Scheduler.Active);
    }

    [Fact]
    public async Task OnMobileDeleted_BoundBrain_UnbindsIt()
    {
        var fixture = new LifecycleFixture();
        var mobile = fixture.AddMobile(0x1, "guard");
        fixture.Scheduler.Bound.Add(mobile.Id);

        await fixture.Subscriber.OnMobileDeleted(new(mobile), CancellationToken.None);

        Assert.Empty(fixture.Scheduler.Bound);
        Assert.Equal([mobile.Id], fixture.Scheduler.Unbound);
    }

    [Fact]
    public async Task OnMobileChangedSector_BoundBrain_UsesTargetSectorActivity()
    {
        var fixture = new LifecycleFixture();
        var mobile = fixture.AddMobile(0x1, "guard");
        fixture.Scheduler.Bound.Add(mobile.Id);
        fixture.Sectors.Active.Add((0, 1, 1));

        await fixture.Subscriber.OnMobileChangedSector(
            new(mobile.Id, 0, 0, 0, 0, 1, 1),
            CancellationToken.None
        );

        Assert.Equal([mobile.Id], fixture.Scheduler.Active);

        await fixture.Subscriber.OnMobileChangedSector(
            new(mobile.Id, 0, 1, 1, 0, 2, 2),
            CancellationToken.None
        );

        Assert.Empty(fixture.Scheduler.Active);
        Assert.Equal([mobile.Id], fixture.Scheduler.Deactivated);
    }

    [Fact]
    public async Task OnSectorEvents_ExactSector_ActivatesAndDeactivatesBrainMobiles()
    {
        var fixture = new LifecycleFixture();
        var first = fixture.AddMobile(0x1, "guard", x: 5, y: 5);
        var second = fixture.AddMobile(0x2, "guard", x: 6, y: 6);
        fixture.AddMobile(0x3, "", x: 7, y: 7);
        fixture.AddMobile(0x4, "guard", x: 32, y: 5);
        fixture.Scheduler.Bound.UnionWith([first.Id, second.Id]);

        await fixture.Subscriber.OnSectorActivated(new(0, 0, 0), CancellationToken.None);
        await fixture.Subscriber.OnSectorDeactivated(new(0, 0, 0), CancellationToken.None);

        Assert.Equal([first.Id, second.Id], fixture.Scheduler.Activated);
        Assert.Equal([first.Id, second.Id], fixture.Scheduler.Deactivated);
    }

    [Fact]
    public async Task OnBrainDefinitionReloaded_MatchingDescriptor_RefreshesScheduler()
    {
        var fixture = new LifecycleFixture();
        var descriptor = new BrainDescriptor("guard", 1000, 8, 10);

        await fixture.Subscriber.OnBrainDefinitionReloaded(new("guard", descriptor), CancellationToken.None);

        Assert.Equal([("guard", descriptor)], fixture.Scheduler.Refreshed);
    }

    [Fact]
    public async Task DuplicateLifecycleEvents_IdempotentSchedulerOperations_LeaveOneBindingAndStateTransition()
    {
        var fixture = new LifecycleFixture();
        var mobile = fixture.AddMobile(0x1, "guard", x: 16, y: 16);
        fixture.Sectors.Active.Add((0, 1, 1));

        await fixture.Subscriber.OnMobileCreated(new(mobile), CancellationToken.None);
        await fixture.Subscriber.OnMobileCreated(new(mobile), CancellationToken.None);
        await fixture.Subscriber.OnMobileChangedSector(new(mobile.Id, 0, 0, 0, 0, 1, 1), CancellationToken.None);
        await fixture.Subscriber.OnMobileChangedSector(new(mobile.Id, 0, 0, 0, 0, 1, 1), CancellationToken.None);
        await fixture.Subscriber.OnMobileChangedSector(new(mobile.Id, 0, 1, 1, 0, 2, 2), CancellationToken.None);
        await fixture.Subscriber.OnMobileChangedSector(new(mobile.Id, 0, 1, 1, 0, 2, 2), CancellationToken.None);

        Assert.Equal([mobile.Id], fixture.Scheduler.Bound);
        Assert.Empty(fixture.Scheduler.Active);
        Assert.Equal([mobile.Id], fixture.Scheduler.Activated);
        Assert.Equal([mobile.Id], fixture.Scheduler.Deactivated);
    }

    [Fact]
    public void Subscribe_RegistersEveryLifecycleEvent()
    {
        var fixture = new LifecycleFixture();
        var eventBus = new RecordingEventBus();

        fixture.Subscriber.Subscribe(eventBus);

        Assert.Equal(
            [
                typeof(WorldReadyEvent),
                typeof(MobileCreatedEvent),
                typeof(MobileDeletedEvent),
                typeof(MobileChangedSectorEvent),
                typeof(SectorActivatedEvent),
                typeof(SectorDeactivatedEvent),
                typeof(BrainDefinitionReloadedEvent)
            ],
            eventBus.Subscribed
        );
    }

    private sealed class LifecycleFixture
    {
        public FakePersistenceService Persistence { get; } = new();

        public RecordingScheduler Scheduler { get; } = new();

        public StubSectorActivityService Sectors { get; } = new();

        public SpatialIndexService Spatial { get; }

        public NpcBrainLifecycleSubscriber Subscriber { get; }

        public LifecycleFixture()
        {
            Spatial = new(Persistence, new StubLoopAffinity(), new StubEventBus());
            Subscriber = new(Spatial, Persistence, Scheduler, Sectors);
        }

        public MobileEntity AddMobile(uint serial, string brainId, int x = 0, int y = 0)
        {
            var mobile = new MobileEntity
            {
                Id = new(serial),
                Name = $"mobile-{serial}",
                BrainScriptId = brainId,
                MapId = 0,
                Position = new(x, y, 0)
            };
            Persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();
            Spatial.AddOrUpdate(mobile);

            return mobile;
        }
    }

    private sealed class RecordingScheduler : INpcBrainScheduler
    {
        public HashSet<Serial> Bound { get; } = [];

        public HashSet<Serial> Active { get; } = [];

        public List<Serial> Unbound { get; } = [];

        public List<Serial> Activated { get; } = [];

        public List<Serial> Deactivated { get; } = [];

        public List<(string BrainId, BrainDescriptor Descriptor)> Refreshed { get; } = [];

        public int MaxHearingRange => 0;

        public int MaxPerceptionRange => 0;

        public void Bind(MobileEntity mobile)
        {
            Bound.Add(mobile.Id);
        }

        public void Unbind(Serial mobileId)
        {
            if (!Bound.Remove(mobileId))
            {
                return;
            }

            Active.Remove(mobileId);
            Unbound.Add(mobileId);
        }

        public void Activate(Serial mobileId)
        {
            if (Bound.Contains(mobileId) && Active.Add(mobileId))
            {
                Activated.Add(mobileId);
            }
        }

        public void Deactivate(Serial mobileId)
        {
            if (Active.Remove(mobileId))
            {
                Deactivated.Add(mobileId);
            }
        }

        public void RefreshDescriptor(string brainId, BrainDescriptor descriptor)
        {
            Refreshed.Add((brainId, descriptor));
        }

        public bool IsActive(Serial mobileId)
            => Active.Contains(mobileId);

        public bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor)
        {
            descriptor = null;
            return false;
        }

        public void EnqueueEvent(Serial mobileId, NpcBrainHookType hook, NpcBrainEvent brainEvent)
        {
        }

        public void Tick()
        {
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

    private sealed class RecordingEventBus : IEventBus
    {
        public List<Type> Subscribed { get; } = [];

        public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        {
        }

        public Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default)
            where TEvent : IEvent
            => Task.CompletedTask;

        public IDisposable RegisterListener<TEvent>(IEventListener<TEvent> listener) where TEvent : IEvent
            => NoopDisposable.Instance;

        public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : IEvent
        {
            Subscribed.Add(typeof(TEvent));
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
