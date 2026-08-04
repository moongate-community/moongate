using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Services.AI;
using Moongate.Tests.Support;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Server.AI;

public class AiActionServiceTests
{
    private sealed class RecordingChatService : IChatService
    {
        public List<(Serial Speaker, ChatMessageType Type, string Text, Hue Hue, int Range)> Messages { get; } = [];

        /// <summary>Set to make every SayAs refuse, standing in for a rule ChatService would enforce.</summary>
        public bool Refuse { get; set; }

        public void Broadcast(string text, Hue? hue = null) { }

        public void SendSystemMessage(PlayerSession session, string text, Hue? hue = null)
        {
        }

        public void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range)
            => Messages.Add((speaker.Id, type, text, hue, range));

        // Records rather than enforcing: the rules belong to ChatService, and the tests that pin them
        // live in ChatServiceTests.
        public bool SayAs(MobileEntity speaker, string text)
        {
            if (Refuse)
            {
                return false;
            }

            Say(speaker, ChatMessageType.Regular, text, Hue.Default, 15);

            return true;
        }
    }

    private sealed class RecordingMovementService : IMovementService
    {
        public List<(Serial MobileId, DirectionType Direction)> Moves { get; } = [];

        public void TryMove(PlayerSession session, DirectionType direction, byte sequence) { }


        public void DrainQueue(PlayerSession session)

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
            => minValue + _result;
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
        var outer = AddOwner(persistence, x: 12, y: 8);
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

    [Theory, InlineData(0x1, true, 0), InlineData(0x2, false, 0), InlineData(0x2, true, 1)]
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
    public void MoveTo_AlreadyAtCoordinate_AcceptsWithoutMoving()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.MoveTo(10, 10));
        }

        Assert.Empty(movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void MoveTo_StepsGreedilyTowardCoordinate()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.MoveTo(14, 8));
        }

        Assert.Equal([(owner.Id, DirectionType.NorthEast)], movement.Moves);
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
    public void Patrol_OutsideLeash_ReturnsTowardHome()
    {
        var (service, persistence, _, movement, metrics) = Build(3);
        var owner = AddOwner(persistence, x: 19, y: 10);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.Patrol());
        }

        Assert.Equal([(owner.Id, DirectionType.West)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

    [Fact]
    public void Patrol_WithinLeash_UsesInjectedRandomDirection()
    {
        var (service, persistence, _, movement, metrics) = Build(3);
        var owner = AddOwner(persistence, x: 12, y: 8);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.Patrol());
        }

        Assert.Equal([(owner.Id, DirectionType.SouthEast)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
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

        using (service.Begin(Context(owner, 0)))
        {
            Assert.False(service.ReturnHome());
        }

        Assert.Empty(movement.Moves);
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

    // Blank and oversize text are refused by ChatService now, not here -- that is what unified the
    // rules with chat.say. The refusals themselves are pinned in ChatServiceTests; what this asserts
    // is that a refusal reaches the brain as a rejected intent rather than being swallowed.
    [Fact]
    public void Say_WhenTheChatServiceRefuses_CountsARejectedIntent()
    {
        var (service, persistence, chat, _, metrics) = Build();
        var owner = AddOwner(persistence);
        chat.Refuse = true;

        using (service.Begin(Context(owner)))
        {
            Assert.False(service.Say("anything at all"));
        }

        Assert.Empty(chat.Messages);
        Assert.Equal(0, metrics.Current.IntentsAccepted);
        Assert.Equal(1, metrics.Current.IntentsRejected);
    }

    [Fact]
    public void Step_MovesInGivenDirection()
    {
        var (service, persistence, _, movement, metrics) = Build();
        var owner = AddOwner(persistence);

        using (service.Begin(Context(owner)))
        {
            Assert.True(service.Step(DirectionType.NorthEast));
        }

        Assert.Equal([(owner.Id, DirectionType.NorthEast)], movement.Moves);
        Assert.Equal(1, metrics.Current.IntentsAccepted);
    }

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

    private static (AiActionService Service, FakePersistenceService Persistence, RecordingChatService Chat,
        RecordingMovementService Movement, NpcAiMetrics Metrics) Build(int randomResult = 0)
    {
        var service = Build(out var persistence, out var chat, out var movement, out var metrics, randomResult);

        return (service, persistence, chat, movement, metrics);
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
}
