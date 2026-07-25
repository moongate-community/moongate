using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Abstractions.Types;
using Moongate.Server.Services.AI;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.AI;

public class BrainIntentExecutorTests
{
    [Fact]
    public void Execute_Idle_AcceptsWithoutMutation()
    {
        var (executor, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence, combatantId: new(0x2), warmode: true);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Idle)]);

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(new Serial(0x2), stored.CombatantId);
        Assert.True(stored.Warmode);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
        Assert.Equal(0, metrics.Current.IntentsRejected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Execute_SayBlankText_Rejects(string? text)
    {
        var (executor, persistence, chat, _, metrics) = Build();
        var owner = AddOwner(persistence);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Say, text: text)]);

        Assert.Empty(chat.Messages);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Execute_SayOversizeText_Rejects()
    {
        var (executor, persistence, chat, _, metrics) = Build();
        var owner = AddOwner(persistence);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Say, text: new('a', 129))]);

        Assert.Empty(chat.Messages);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Execute_SayValidText_UsesRegularSpeechDefaultHueAndRange15()
    {
        var (executor, persistence, chat, _, metrics) = Build();
        var owner = AddOwner(persistence);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Say, text: "Halt!")]);

        var message = Assert.Single(chat.Messages);
        Assert.Equal(owner.Id, message.Speaker);
        Assert.Equal(ChatMessageType.Regular, message.Type);
        Assert.Equal("Halt!", message.Text);
        Assert.Equal(Hue.Default, message.Hue);
        Assert.Equal(15, message.Range);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Execute_EngageVisibleTarget_SetsCombatPostureWithoutMovingOrAttacking()
    {
        var (executor, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 12, 10);

        executor.Execute(owner.Id, Context(owner, nearby: [Snapshot(target)]), [Intent(BrainIntentType.Engage, target.Id)]);

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(target.Id, stored.CombatantId);
        Assert.True(stored.Warmode);
        Assert.Empty(movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Theory]
    [InlineData(0x1, true, 0)]
    [InlineData(0x2, false, 0)]
    [InlineData(0x2, true, 1)]
    public void Execute_EngageInvalidTarget_Rejects(uint targetId, bool includeNearby, int targetMapId)
    {
        var (executor, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = targetId == owner.Id.Value ? owner : AddMobile(persistence, targetId, targetMapId, 12, 10);
        var nearby = includeNearby ? new[] { Snapshot(target) } : [];

        executor.Execute(owner.Id, Context(owner, nearby: nearby), [Intent(BrainIntentType.Engage, new(targetId))]);

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(Serial.Zero, stored.CombatantId);
        Assert.False(stored.Warmode);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Execute_EngageMissingTarget_Rejects()
    {
        var (executor, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Engage)]);

        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Execute_ClearTarget_ClearsCombatPostureAndPersists()
    {
        var (executor, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence, combatantId: new(0x2), warmode: true);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.ClearTarget)]);

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(Serial.Zero, stored.CombatantId);
        Assert.False(stored.Warmode);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Theory]
    [InlineData(BrainIntentType.MoveToward, 14, 8, DirectionType.NorthEast)]
    [InlineData(BrainIntentType.MoveAway, 14, 8, DirectionType.SouthWest)]
    public void Execute_MoveIntent_UsesEightWayDirection(BrainIntentType type, int targetX, int targetY, DirectionType expected)
    {
        var (executor, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, targetX, targetY);

        executor.Execute(owner.Id, Context(owner, nearby: [Snapshot(target)]), [Intent(type, target.Id)]);

        Assert.Equal([(owner.Id, expected)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Execute_MoveTowardEqualPosition_AcceptsWithoutDelegating()
    {
        var (executor, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 10, 10);

        executor.Execute(owner.Id, Context(owner, nearby: [Snapshot(target)]), [Intent(BrainIntentType.MoveToward, target.Id)]);

        Assert.Empty(movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Theory]
    [InlineData(BrainIntentType.MoveToward)]
    [InlineData(BrainIntentType.MoveAway)]
    public void Execute_MoveTargetOutsidePerception_Rejects(BrainIntentType type)
    {
        var (executor, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 12, 10);

        executor.Execute(owner.Id, Context(owner), [Intent(type, target.Id)]);

        Assert.Empty(movement.Moves);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Execute_ReturnHome_OnHomeMapMovesTowardHome()
    {
        var (executor, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);

        executor.Execute(owner.Id, Context(owner, homePosition: new(8, 12, 0)), [Intent(BrainIntentType.ReturnHome)]);

        Assert.Equal([(owner.Id, DirectionType.SouthWest)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Execute_ReturnHomeOutsideHomeMap_Rejects()
    {
        var (executor, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence, mapId: 1);

        executor.Execute(owner.Id, Context(owner, homeMapId: 0), [Intent(BrainIntentType.ReturnHome)]);

        Assert.Empty(movement.Moves);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Execute_PatrolWithinLeash_UsesInjectedRandomDirection()
    {
        var (executor, persistence, _, movement, metrics) = Build(randomResult: 3);
        var owner = AddOwner(persistence, x: 12, y: 8);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Patrol)]);

        Assert.Equal([(owner.Id, DirectionType.SouthEast)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Execute_PatrolOutsideLeash_ReturnsTowardHome()
    {
        var (executor, persistence, _, movement, metrics) = Build(randomResult: 3);
        var owner = AddOwner(persistence, x: 19, y: 10);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Patrol)]);

        Assert.Equal([(owner.Id, DirectionType.West)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Execute_UnknownIntent_RejectsWithoutThrowing()
    {
        var (executor, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence);

        executor.Execute(owner.Id, Context(owner), [Intent(BrainIntentType.Unknown, rawType: "explode")]);

        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Execute_OrderedIntents_ExecutesEachInDeclaredOrderAndRevalidates()
    {
        var (executor, persistence, chat, movement, metrics) = Build();
        var owner = AddOwner(persistence, combatantId: new(0x2), warmode: true);
        var target = AddMobile(persistence, 0x2, 0, 11, 10);

        executor.Execute(
            owner.Id,
            Context(owner, nearby: [Snapshot(target)]),
            [
                Intent(BrainIntentType.ClearTarget),
                Intent(BrainIntentType.MoveToward, target.Id),
                Intent(BrainIntentType.Say, text: "Advance")
            ]
        );

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(Serial.Zero, stored.CombatantId);
        Assert.False(stored.Warmode);
        Assert.Equal([(owner.Id, DirectionType.East)], movement.Moves);
        Assert.Equal("Advance", Assert.Single(chat.Messages).Text);
        Assert.Equal(3, metrics.Current.IntentsAccepted);
    }

    private static BrainIntentExecutor Build(
        out FakePersistenceService persistence,
        out RecordingChatService chat,
        out RecordingMovementService movement,
        out NpcAiMetrics metrics,
        int randomResult = 0
    )
    {
        persistence = new();
        chat = new();
        movement = new();
        metrics = new();

        return new(persistence, movement, chat, new FixedRandom(randomResult), new StubLoopAffinity(), metrics);
    }

    private static (BrainIntentExecutor Executor, FakePersistenceService Persistence, RecordingChatService Chat, RecordingMovementService Movement, NpcAiMetrics Metrics) Build(int randomResult = 0)
    {
        var executor = Build(out var persistence, out var chat, out var movement, out var metrics, randomResult);
        return (executor, persistence, chat, movement, metrics);
    }

    private static MobileEntity AddOwner(
        FakePersistenceService persistence,
        uint id = 0x1,
        int mapId = 0,
        int x = 10,
        int y = 10,
        Serial? combatantId = null,
        bool warmode = false
    )
        => AddMobile(persistence, id, mapId, x, y, combatantId, warmode);

    private static MobileEntity AddMobile(
        FakePersistenceService persistence,
        uint id,
        int mapId,
        int x,
        int y,
        Serial? combatantId = null,
        bool warmode = false
    )
    {
        var mobile = new MobileEntity
        {
            Id = new(id),
            Name = "Test mobile",
            MapId = mapId,
            Position = new(x, y, 0),
            CombatantId = combatantId ?? Serial.Zero,
            Warmode = warmode
        };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).AsTask().Wait();
        return mobile;
    }

    private static BrainContext Context(
        MobileEntity owner,
        int? homeMapId = null,
        Point3D? homePosition = null,
        IReadOnlyList<BrainMobileSnapshot>? nearby = null
    )
        => new(
            DateTimeOffset.UtcNow,
            Snapshot(owner),
            homeMapId ?? owner.MapId,
            homePosition ?? new(10, 10, 0),
            nearby ?? []
        );

    private static BrainIntent Intent(BrainIntentType type, Serial? targetId = null, string? text = null, string rawType = "test")
        => new(type, rawType, targetId ?? Serial.Zero, text);

    private static BrainMobileSnapshot Snapshot(MobileEntity mobile)
        => new(
            mobile.Id,
            mobile.Name,
            false,
            mobile.MapId,
            mobile.Position,
            mobile.Hits,
            mobile.HitsMax,
            mobile.Warmode,
            mobile.CombatantId,
            mobile.Criminal,
            mobile.Kills
        );

    private sealed class RecordingChatService : IChatService
    {
        public List<(Serial Speaker, ChatMessageType Type, string Text, Hue Hue, int Range)> Messages { get; } = [];

        public void Broadcast(string text, Hue? hue = null)
        {
        }

        public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
        {
            Messages.Add((speaker.Id, type, text, hue, range));
        }
    }

    private sealed class RecordingMovementService : IMovementService
    {
        public List<(Serial MobileId, DirectionType Direction)> Moves { get; } = [];

        public void TryMove(Moongate.Server.Abstractions.Data.Session.PlayerSession session, DirectionType direction, byte sequence)
        {
        }

        public bool TryMoveNpc(Serial mobileId, DirectionType direction)
        {
            Moves.Add((mobileId, direction));
            return true;
        }
    }

    private sealed class FixedRandom : Random
    {
        private readonly int _result;

        public FixedRandom(int result)
        {
            _result = result;
        }

        public override int Next(int minValue, int maxValue)
        {
            return minValue + _result;
        }
    }
}
