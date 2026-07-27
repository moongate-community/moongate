using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Services.AI;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.AI;

public class AiActionServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Say_BlankText_Rejects(string? text)
    {
        var (service, persistence, chat, _, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner)))
        {
            Assert.False(service.Say(text!));
        }

        Assert.Empty(chat.Messages);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Say_OversizeText_Rejects()
    {
        var (service, persistence, chat, _, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner)))
        {
            Assert.False(service.Say(new('a', 129)));
        }

        Assert.Empty(chat.Messages);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Say_ValidText_UsesRegularSpeechDefaultHueAndRange15()
    {
        var (service, persistence, chat, _, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.Say("Halt!"));
        }

        var message = Assert.Single(chat.Messages);
        Assert.Equal(owner.Id, message.Speaker);
        Assert.Equal(ChatMessageType.Regular, message.Type);
        Assert.Equal("Halt!", message.Text);
        Assert.Equal(Hue.Default, message.Hue);
        Assert.Equal(15, message.Range);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Engage_VisibleTarget_SetsCombatPostureWithoutMovingOrAttacking()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 12, 10);

        using (service.Begin(Context(owner, nearby: [Snapshot(target)])))
        {
            Assert.True(service.Engage(target.Id));
        }

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
    public void Engage_InvalidTarget_Rejects(uint targetId, bool includeNearby, int targetMapId)
    {
        var (service, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = targetId == owner.Id.Value ? owner : AddMobile(persistence, targetId, targetMapId, 12, 10);
        var nearby = includeNearby ? new[] { Snapshot(target) } : [];

        using (service.Begin(Context(owner, nearby: nearby)))
        {
            Assert.False(service.Engage(new(targetId)));
        }

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(Serial.Zero, stored.CombatantId);
        Assert.False(stored.Warmode);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Engage_MissingTarget_Rejects()
    {
        var (service, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner)))
        {
            Assert.False(service.Engage(Serial.Zero));
        }

        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void ClearTarget_ClearsCombatPostureAndPersists()
    {
        var (service, persistence, _, _, metrics) = Build();
        var owner = AddOwner(persistence, combatantId: new(0x2), warmode: true);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.ClearTarget());
        }

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(Serial.Zero, stored.CombatantId);
        Assert.False(stored.Warmode);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void MoveToward_UsesEightWayDirection()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 14, 8);

        using (service.Begin(Context(owner, nearby: [Snapshot(target)])))
        {
            Assert.True(service.MoveToward(target.Id));
        }

        Assert.Equal([(owner.Id, DirectionType.NorthEast)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void MoveAway_UsesEightWayDirection()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 14, 8);

        using (service.Begin(Context(owner, nearby: [Snapshot(target)])))
        {
            Assert.True(service.MoveAway(target.Id));
        }

        Assert.Equal([(owner.Id, DirectionType.SouthWest)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void MoveToward_EqualPosition_AcceptsWithoutDelegating()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 10, 10);

        using (service.Begin(Context(owner, nearby: [Snapshot(target)])))
        {
            Assert.True(service.MoveToward(target.Id));
        }

        Assert.Empty(movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void MoveToward_TargetOutsidePerception_Rejects()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);
        var target = AddMobile(persistence, 0x2, 0, 12, 10);

        using (service.Begin(Context(owner)))
        {
            Assert.False(service.MoveToward(target.Id));
            Assert.False(service.MoveAway(target.Id));
        }

        Assert.Empty(movement.Moves);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(2, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void ReturnHome_OnHomeMapMovesTowardHome()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner, homePosition: new(8, 12, 0))))
        {
            Assert.True(service.ReturnHome());
        }

        Assert.Equal([(owner.Id, DirectionType.SouthWest)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void ReturnHome_OutsideHomeMap_Rejects()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence, mapId: 1);

        using (service.Begin(Context(owner, homeMapId: 0)))
        {
            Assert.False(service.ReturnHome());
        }

        Assert.Empty(movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Patrol_WithinLeash_UsesInjectedRandomDirection()
    {
        var (service, persistence, _, movement, metrics) = Build(randomResult: 3);
        var owner = AddOwner(persistence, x: 12, y: 8);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.Patrol());
        }

        Assert.Equal([(owner.Id, DirectionType.SouthEast)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Patrol_OutsideLeash_ReturnsTowardHome()
    {
        var (service, persistence, _, movement, metrics) = Build(randomResult: 3);
        var owner = AddOwner(persistence, x: 19, y: 10);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.Patrol());
        }

        Assert.Equal([(owner.Id, DirectionType.West)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void OrderedActions_ApplyInSequenceAndRevalidate()
    {
        var (service, persistence, chat, movement, metrics) = Build();
        var owner = AddOwner(persistence, combatantId: new(0x2), warmode: true);
        var target = AddMobile(persistence, 0x2, 0, 11, 10);

        using (service.Begin(Context(owner, nearby: [Snapshot(target)])))
        {
            Assert.True(service.ClearTarget());
            Assert.True(service.MoveToward(target.Id));
            Assert.True(service.Say("Advance"));
        }

        var stored = persistence.Store<MobileEntity>().GetById(owner.Id)!;
        Assert.Equal(Serial.Zero, stored.CombatantId);
        Assert.False(stored.Warmode);
        Assert.Equal([(owner.Id, DirectionType.East)], movement.Moves);
        Assert.Equal("Advance", Assert.Single(chat.Messages).Text);
        Assert.Equal(3, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Action_WithoutActiveContext_Throws()
    {
        var (service, _, _, _, _) = Build();

        var exception = Assert.Throws<InvalidOperationException>(() => service.Patrol());
        Assert.Contains("brain tick", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Begin_RestoresPreviousContextOnDispose()
    {
        var (service, persistence, _, _, _) = Build();
        var outer = AddOwner(persistence, id: 0x1, x: 12, y: 8);
        var inner = AddMobile(persistence, 0x2, 0, 12, 8);

        using (service.Begin(Context(outer)))
        {
            using (service.Begin(Context(inner)))
            {
                Assert.True(service.Patrol());
            }

            Assert.True(service.Patrol());
        }

        Assert.Throws<InvalidOperationException>(() => service.Patrol());
    }

    private static AiActionService Build(
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

    private static (AiActionService Service, FakePersistenceService Persistence, RecordingChatService Chat, RecordingMovementService Movement, NpcAiMetrics Metrics) Build(int randomResult = 0)
    {
        var service = Build(out var persistence, out var chat, out var movement, out var metrics, randomResult);
        return (service, persistence, chat, movement, metrics);
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
