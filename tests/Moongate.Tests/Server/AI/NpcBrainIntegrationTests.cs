using DryIoc;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Scripting.AI;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Config;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.AI;
using Moongate.Server.Services.World;
using Moongate.Server.Subscribers;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Core.Directories;
using SquidStd.Scripting.Lua.Data.Config;
using SquidStd.Scripting.Lua.Services;
using SquidStd.Services.Core.Services;

namespace Moongate.Tests.Server.AI;

public sealed class NpcBrainIntegrationTests
{
    private const string FirstMeetingReply = "I haven't seen you before.";
    private const string ReturningPlayerReply = "Welcome back.";

    [Fact]
    public async Task PlayerSpeech_CompleteGuardLifecycle_RepliesWithRetainedMemory()
    {
        using var fixture = new IntegrationFixture();
        var hookOffset = 0;

        // 1. Seed one played character and one guard using the real built-in brain.
        await fixture.SeedAsync();

        // 2. Indexing mobiles alone does not activate sectors: only player coverage does.
        fixture.Spatial.AddOrUpdate(fixture.Player);
        fixture.Spatial.AddOrUpdate(fixture.Guard);
        Assert.Equal(new(0, 0), fixture.Activity.Current);
        Assert.False(fixture.Activity.IsActive(0, 2, 2));

        // 3. World lifecycle binds the guard, but its inactive sector leaves it asleep.
        fixture.Bus.Publish(new WorldReadyEvent());
        fixture.Scheduler.Tick();
        Assert.False(fixture.Scheduler.IsActive(fixture.Guard.Id));
        Assert.True(fixture.Scheduler.TryGetDescriptor(fixture.Guard.Id, out var sleepingDescriptor));
        Assert.Equal("guard", sleepingDescriptor!.BrainId);
        Assert.Equal(0, fixture.Metrics.Current.HookInvocations);
        Assert.Equal(0, fixture.Metrics.Current.IntentsAccepted);
        Assert.Empty(fixture.Runtime.Invocations);
        Assert.Empty(fixture.Chat.Messages);
        Assert.Empty(fixture.Chat.Broadcasts);

        // 4. A player activates exactly the same-map 3×3 sector coverage.
        fixture.Activity.TrackPlayer(fixture.Player);
        Assert.Equal(new(9, 0), fixture.Activity.Current);

        for (var sectorX = 1; sectorX <= 3; sectorX++)
        {
            for (var sectorY = 1; sectorY <= 3; sectorY++)
            {
                Assert.True(fixture.Activity.IsActive(0, sectorX, sectorY));
            }
        }

        Assert.False(fixture.Activity.IsActive(0, 0, 2));
        Assert.False(fixture.Activity.IsActive(0, 4, 2));
        Assert.False(fixture.Activity.IsActive(1, 2, 2));

        // 5. The first wake invokes activate, the seeded range event, and think; think returns idle.
        var intentsBeforeActivation = fixture.Metrics.Current.IntentsAccepted;
        fixture.Scheduler.Tick();
        Assert.True(fixture.Scheduler.IsActive(fixture.Guard.Id));
        AssertNextHooks(
            fixture,
            ref hookOffset,
            NpcBrainHookType.Activate,
            NpcBrainHookType.MobileEnteredRange,
            NpcBrainHookType.Think
        );
        Assert.Equal(intentsBeforeActivation + 1, fixture.Metrics.Current.IntentsAccepted);

        // 6. Speech inside the guard's 15-tile hearing range only reaches its mailbox.
        var hooksBeforeFirstSpeech = fixture.Runtime.Invocations.Count;
        fixture.Bus.Publish(new MobileSpeechEvent(fixture.Player.Id, ChatMessageType.Regular, "ciao"));
        Assert.Equal(hooksBeforeFirstSpeech, fixture.Runtime.Invocations.Count);
        Assert.Empty(fixture.Chat.Messages);
        Assert.Empty(fixture.Chat.Broadcasts);

        // 7. The scheduler is the component that invokes Lua and applies the returned intent.
        fixture.Scheduler.Tick();
        AssertNextHooks(fixture, ref hookOffset, NpcBrainHookType.SpeechHeard);

        // 8. The real C# intent executor performs the single chat mutation.
        Assert.Equal(
            [(fixture.Guard.Id, ChatMessageType.Regular, FirstMeetingReply, Hue.Default, 15)],
            fixture.Chat.Messages
        );
        Assert.Empty(fixture.Chat.Broadcasts);

        // 9. A second routed speech observes the guard's retained per-mobile blackboard.
        fixture.Bus.Publish(new MobileSpeechEvent(fixture.Player.Id, ChatMessageType.Regular, "ciao"));
        Assert.Equal(
            [(fixture.Guard.Id, ChatMessageType.Regular, FirstMeetingReply, Hue.Default, 15)],
            fixture.Chat.Messages
        );
        fixture.Scheduler.Tick();
        AssertNextHooks(fixture, ref hookOffset, NpcBrainHookType.SpeechHeard);
        Assert.Equal(
            [
                (fixture.Guard.Id, ChatMessageType.Regular, FirstMeetingReply, Hue.Default, 15),
                (fixture.Guard.Id, ChatMessageType.Regular, ReturningPlayerReply, Hue.Default, 15)
            ],
            fixture.Chat.Messages
        );
        Assert.Empty(fixture.Chat.Broadcasts);

        // 10. Removing the player starts grace; at 59 seconds the guard remains active and thinks.
        fixture.Activity.UntrackPlayer(fixture.Player.Id);
        Assert.Equal(new(0, 9), fixture.Activity.Current);
        fixture.Time.Advance(TimeSpan.FromSeconds(59));
        fixture.Activity.Tick();
        Assert.True(fixture.Activity.IsActive(0, 2, 2));
        Assert.True(fixture.Scheduler.IsActive(fixture.Guard.Id));
        var intentsBeforeGraceTick = fixture.Metrics.Current.IntentsAccepted;
        fixture.Scheduler.Tick();
        AssertNextHooks(fixture, ref hookOffset, NpcBrainHookType.Think);
        Assert.Equal(intentsBeforeGraceTick + 1, fixture.Metrics.Current.IntentsAccepted);
        Assert.True(fixture.Scheduler.IsActive(fixture.Guard.Id));
        Assert.Equal(
            [
                (fixture.Guard.Id, ChatMessageType.Regular, FirstMeetingReply, Hue.Default, 15),
                (fixture.Guard.Id, ChatMessageType.Regular, ReturningPlayerReply, Hue.Default, 15)
            ],
            fixture.Chat.Messages
        );
        Assert.Empty(fixture.Chat.Broadcasts);

        // 11. At exactly 60 seconds the sector deactivates; one deactivate hook runs, then it sleeps.
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        fixture.Activity.Tick();
        Assert.False(fixture.Activity.IsActive(0, 2, 2));
        Assert.False(fixture.Scheduler.IsActive(fixture.Guard.Id));
        fixture.Scheduler.Tick();
        AssertNextHooks(fixture, ref hookOffset, NpcBrainHookType.Deactivate);
        Assert.False(fixture.Scheduler.IsActive(fixture.Guard.Id));
        Assert.Equal(1, fixture.Metrics.Current.SleepingBrains);
        Assert.True(fixture.Scheduler.TryGetDescriptor(fixture.Guard.Id, out var retainedDescriptor));
        Assert.Equal("guard", retainedDescriptor!.BrainId);
        Assert.Equal(
            [
                (fixture.Guard.Id, ChatMessageType.Regular, FirstMeetingReply, Hue.Default, 15),
                (fixture.Guard.Id, ChatMessageType.Regular, ReturningPlayerReply, Hue.Default, 15)
            ],
            fixture.Chat.Messages
        );
        Assert.Empty(fixture.Chat.Broadcasts);

        // 12. Re-tracking wakes the same binding; its blackboard still recognizes the player.
        fixture.Activity.TrackPlayer(fixture.Player);
        Assert.True(fixture.Scheduler.IsActive(fixture.Guard.Id));
        var intentsBeforeWake = fixture.Metrics.Current.IntentsAccepted;
        fixture.Scheduler.Tick();
        AssertNextHooks(
            fixture,
            ref hookOffset,
            NpcBrainHookType.Activate,
            NpcBrainHookType.MobileEnteredRange,
            NpcBrainHookType.Think
        );
        Assert.Equal(intentsBeforeWake + 1, fixture.Metrics.Current.IntentsAccepted);
        fixture.Bus.Publish(new MobileSpeechEvent(fixture.Player.Id, ChatMessageType.Regular, "ciao"));
        Assert.Equal(2, fixture.Chat.Messages.Count);
        fixture.Scheduler.Tick();
        AssertNextHooks(fixture, ref hookOffset, NpcBrainHookType.SpeechHeard);
        Assert.Equal(
            [
                (fixture.Guard.Id, ChatMessageType.Regular, FirstMeetingReply, Hue.Default, 15),
                (fixture.Guard.Id, ChatMessageType.Regular, ReturningPlayerReply, Hue.Default, 15),
                (fixture.Guard.Id, ChatMessageType.Regular, ReturningPlayerReply, Hue.Default, 15)
            ],
            fixture.Chat.Messages
        );
        Assert.Empty(fixture.Chat.Broadcasts);
    }

    private static void AssertNextHooks(
        IntegrationFixture fixture,
        ref int offset,
        params NpcBrainHookType[] expected
    )
    {
        Assert.Equal(expected, fixture.Runtime.Invocations.Skip(offset));
        offset += expected.Length;
        Assert.Equal(offset, fixture.Runtime.Invocations.Count);
    }

    private sealed class IntegrationFixture : IDisposable
    {
        private readonly Container _container = new();
        private readonly LuaScriptEngineService _engine;
        private readonly LuaNpcBrainRuntime _luaRuntime;
        private readonly string _root;

        public EventBusService Bus { get; } = new();

        public RecordingChatService Chat { get; } = new();

        public MoongateConfig Config { get; } = new();

        public MobileEntity Guard { get; } = new()
        {
            Id = new(0x2),
            Name = "Guard",
            BrainScriptId = "guard",
            MapId = 0,
            Position = new(40, 32, 0),
            Hits = 100,
            HitsMax = 100
        };

        public StubGameLoopContext Loop { get; } = new();

        public NpcAiMetrics Metrics { get; } = new();

        public FakePersistenceService Persistence { get; } = new();

        public MobileEntity Player { get; } = new()
        {
            Id = new(0x1),
            Name = "Player",
            MapId = 0,
            Position = new(32, 32, 0),
            Hits = 100,
            HitsMax = 100
        };

        public StubSessionManager Sessions { get; } = new();

        public MutableTimeProvider Time { get; } = new(
            new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero)
        );

        public SectorActivityService Activity { get; }

        public RecordingLuaNpcBrainRuntime Runtime { get; }

        public NpcBrainScheduler Scheduler { get; }

        public SpatialIndexService Spatial { get; }

        public IntegrationFixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "mg-npc-brain-integration-" + Guid.NewGuid().ToString("N"));
            var directories = new DirectoriesConfig(_root, ["scripts"]);
            _engine = new(
                directories,
                _container,
                new LuaEngineConfig(_root, directories.GetPath("scripts"), "MoongateTests", "1.0.0")
            );
            BrainAssetSeeder.SeedMissing(Path.Combine(directories.GetPath("scripts"), "brains"));

            var loopAffinity = new StubLoopAffinity();
            Spatial = new(Persistence, loopAffinity, Bus);
            Activity = new(Loop, Bus, Time, Config, Metrics);
            _luaRuntime = new(_engine, directories, Config, Metrics, Loop, Bus, Time);
            Runtime = new(_luaRuntime);
            var contextFactory = new NpcBrainContextFactory(Spatial, Sessions, Time);
            var intentExecutor = new BrainIntentExecutor(
                Persistence,
                new UnexpectedMovementService(),
                Chat,
                new Random(42),
                loopAffinity,
                Metrics
            );
            Scheduler = new(
                Loop,
                Runtime,
                intentExecutor,
                Activity,
                Persistence,
                contextFactory,
                Time,
                Config,
                Metrics
            );

            new NpcBrainLifecycleSubscriber(Spatial, Persistence, Scheduler, Activity).Subscribe(Bus);
            new NpcBrainEventRouter(Spatial, Persistence, Sessions, Scheduler, Activity).Subscribe(Bus);
        }

        public async Task SeedAsync()
        {
            await Persistence.Store<MobileEntity>().UpsertAsync(Player);
            await Persistence.Store<MobileEntity>().UpsertAsync(Guard);
            await Persistence.Store<AccountEntity>().UpsertAsync(
                new()
                {
                    Id = new(0x100),
                    Username = "player",
                    MobileIds = [Player.Id]
                }
            );
            Sessions.Played.Add(Player.Id);
        }

        public void Dispose()
        {
            _luaRuntime.Dispose();
            _engine.Dispose();
            Bus.Dispose();
            _container.Dispose();
            Directory.Delete(_root, true);
        }
    }

    private sealed class RecordingLuaNpcBrainRuntime : INpcBrainRuntime
    {
        private readonly LuaNpcBrainRuntime _inner;
        private readonly List<NpcBrainHookType> _invocations = [];

        public IReadOnlyList<NpcBrainHookType> Invocations => _invocations;

        public RecordingLuaNpcBrainRuntime(LuaNpcBrainRuntime inner)
        {
            _inner = inner;
        }

        public bool TryBind(
            Serial mobileId,
            string brainId,
            out BrainDescriptor? descriptor,
            out string? error
        )
            => _inner.TryBind(mobileId, brainId, out descriptor, out error);

        public bool TryGetDescriptor(Serial mobileId, out BrainDescriptor? descriptor)
            => _inner.TryGetDescriptor(mobileId, out descriptor);

        public NpcBrainInvocationResult Invoke(
            Serial mobileId,
            NpcBrainHookType hook,
            BrainContext context,
            NpcBrainEvent? brainEvent = null
        )
        {
            _invocations.Add(hook);

            return _inner.Invoke(mobileId, hook, context, brainEvent);
        }

        public bool TryReload(
            string brainId,
            out BrainDescriptor? descriptor,
            out string? error
        )
            => _inner.TryReload(brainId, out descriptor, out error);

        public void Reset(Serial mobileId)
        {
            _inner.Reset(mobileId);
        }

        public void Unbind(Serial mobileId)
        {
            _inner.Unbind(mobileId);
        }
    }

    private sealed class UnexpectedMovementService : IMovementService
    {
        public void TryMove(PlayerSession session, DirectionType direction, byte sequence)
        {
            throw new InvalidOperationException("The guard integration flow must not move a player.");
        }

        public bool TryMoveNpc(Serial mobileId, DirectionType direction)
        {
            throw new InvalidOperationException("The built-in guard flow must not emit movement intents.");
        }
    }
}
