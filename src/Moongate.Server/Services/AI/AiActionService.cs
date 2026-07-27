using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using Serilog;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using ILogger = Serilog.ILogger;

namespace Moongate.Server.Services.AI;

/// <summary>Applies NPC brain actions against the mobile of the active tick, resolved from an ambient context.</summary>
public sealed class AiActionService : IAiActionService
{
    private const int SpeechRange = 15;
    private const int SpeechMaximumLength = 128;
    private const int HomeLeashRange = 8;

    private readonly ILogger _logger = Log.ForContext<AiActionService>();
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IMovementService _movement;
    private readonly IChatService _chat;
    private readonly Random _random;
    private readonly ILoopAffinity _loopAffinity;
    private readonly INpcAiMetrics _metrics;

    private BrainContext? _current;

    public AiActionService(
        IPersistenceService persistenceService,
        IMovementService movement,
        IChatService chat,
        Random random,
        ILoopAffinity loopAffinity,
        INpcAiMetrics metrics
    )
    {
        _mobiles = persistenceService.GetStore<MobileEntity, Serial>();
        _movement = movement;
        _chat = chat;
        _random = random;
        _loopAffinity = loopAffinity;
        _metrics = metrics;
    }

    public IDisposable Begin(BrainContext context)
    {
        _loopAffinity.AssertOnLoop("ai.begin");
        var previous = _current;
        _current = context;
        return new ContextScope(this, previous);
    }

    public bool Say(string text)
        => Run((owner, _) => TrySay(owner, text, out var reason) ? Ok() : Fail(reason));

    public bool Patrol()
        => Run((owner, context) => TryPatrol(owner, context, out var reason) ? Ok() : Fail(reason));

    public bool ReturnHome()
        => Run((owner, context) => TryReturnHome(owner, context, out var reason) ? Ok() : Fail(reason));

    public bool MoveToward(Serial targetId)
        => Run((owner, context) => TryMove(owner, context, targetId, false, out var reason) ? Ok() : Fail(reason));

    public bool MoveAway(Serial targetId)
        => Run((owner, context) => TryMove(owner, context, targetId, true, out var reason) ? Ok() : Fail(reason));

    public bool Engage(Serial targetId)
        => Run((owner, context) => TryEngage(owner, context, targetId, out var reason) ? Ok() : Fail(reason));

    public bool ClearTarget()
        => Run((owner, _) => TryClearTarget(owner, out var reason) ? Ok() : Fail(reason));

    public bool Step(DirectionType direction)
        => Run((owner, _) => _movement.TryMoveNpc(owner.Id, direction) ? Ok() : Fail("movement service rejected the step"));

    public bool MoveTo(int x, int y)
        => Run((owner, _) => TryMoveToPosition(owner, new Point3D(x, y, owner.Position.Z), out var reason) ? Ok() : Fail(reason));

    private bool Run(Func<MobileEntity, BrainContext, (bool Ok, string Reason)> action)
    {
        _loopAffinity.AssertOnLoop("ai.action");

        var context = _current
            ?? throw new InvalidOperationException("ai.* is only callable inside an NPC brain tick.");

        var owner = _mobiles.GetById(context.Self.Id);

        if (owner is null)
        {
            _metrics.RecordIntent(false);
            _logger.Debug("Rejected NPC brain action {MobileId}: owner is missing", context.Self.Id);
            return false;
        }

        var (ok, reason) = action(owner, context);

        _metrics.RecordIntent(ok);

        if (!ok)
        {
            _logger.Debug("Rejected NPC brain action {MobileId}: {Reason}", owner.Id, reason);
        }

        return ok;
    }

    private static (bool Ok, string Reason) Ok()
        => (true, "");

    private static (bool Ok, string Reason) Fail(string reason)
        => (false, reason);

    private bool TrySay(MobileEntity owner, string? text, out string reason)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            reason = "speech text is blank";
            return false;
        }

        if (text.Length > SpeechMaximumLength)
        {
            reason = "speech text exceeds the maximum length";
            return false;
        }

        _chat.Say(owner, ChatMessageType.Regular, text, Hue.Default, SpeechRange);
        reason = "";
        return true;
    }

    private bool TryEngage(MobileEntity owner, BrainContext context, Serial targetId, out string reason)
    {
        if (!TryGetPerceivedTarget(owner, context, targetId, out var target, out reason))
        {
            return false;
        }

        if (owner.CombatantId != target.Id || !owner.Warmode)
        {
            owner.CombatantId = target.Id;
            owner.Warmode = true;
            _mobiles.UpsertAsync(owner).WaitSync();
        }

        reason = "";
        return true;
    }

    private bool TryClearTarget(MobileEntity owner, out string reason)
    {
        if (owner.CombatantId != Serial.Zero || owner.Warmode)
        {
            owner.CombatantId = Serial.Zero;
            owner.Warmode = false;
            _mobiles.UpsertAsync(owner).WaitSync();
        }

        reason = "";
        return true;
    }

    private bool TryMove(
        MobileEntity owner,
        BrainContext context,
        Serial targetId,
        bool moveAway,
        out string reason
    )
    {
        if (!TryGetPerceivedTarget(owner, context, targetId, out var target, out reason))
        {
            return false;
        }

        var direction = DirectionToward(owner.Position, target.Position);

        if (owner.Position == target.Position)
        {
            reason = "";
            return true;
        }

        if (moveAway)
        {
            direction = direction.Opposite();
        }

        if (!_movement.TryMoveNpc(owner.Id, direction))
        {
            reason = "movement service rejected the move";
            return false;
        }

        reason = "";
        return true;
    }

    private bool TryReturnHome(MobileEntity owner, BrainContext context, out string reason)
    {
        if (owner.MapId != context.HomeMapId)
        {
            reason = "owner is not on the home map";
            return false;
        }

        return TryMoveToPosition(owner, context.HomePosition, out reason);
    }

    private bool TryPatrol(MobileEntity owner, BrainContext context, out string reason)
    {
        if (owner.MapId != context.HomeMapId)
        {
            reason = "owner is not on the home map";
            return false;
        }

        if (!owner.Position.InRange(context.HomePosition, HomeLeashRange))
        {
            return TryMoveToPosition(owner, context.HomePosition, out reason);
        }

        if (!_movement.TryMoveNpc(owner.Id, (DirectionType)_random.Next(0, 8)))
        {
            reason = "movement service rejected the patrol move";
            return false;
        }

        reason = "";
        return true;
    }

    private bool TryMoveToPosition(MobileEntity owner, Point3D target, out string reason)
    {
        if (owner.Position == target)
        {
            reason = "";
            return true;
        }

        if (!_movement.TryMoveNpc(owner.Id, DirectionToward(owner.Position, target)))
        {
            reason = "movement service rejected the move";
            return false;
        }

        reason = "";
        return true;
    }

    private bool TryGetPerceivedTarget(
        MobileEntity owner,
        BrainContext context,
        Serial targetId,
        [NotNullWhen(true)] out MobileEntity? target,
        out string reason
    )
    {
        target = null;

        if (targetId == Serial.Zero)
        {
            reason = "target is missing";
            return false;
        }

        if (targetId == owner.Id)
        {
            reason = "owner cannot target itself";
            return false;
        }

        if (context.Nearby is null || !context.Nearby.Any(snapshot => snapshot.Id == targetId))
        {
            reason = "target is outside current perception";
            return false;
        }

        var resolvedTarget = _mobiles.GetById(targetId);

        if (resolvedTarget is null)
        {
            reason = "target is missing";
            return false;
        }

        if (resolvedTarget.MapId != owner.MapId)
        {
            reason = "target is on another map";
            return false;
        }

        target = resolvedTarget;
        reason = "";
        return true;
    }

    private static DirectionType DirectionToward(Point3D from, Point3D to)
        => (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y)) switch
        {
            (0, -1) => DirectionType.North,
            (1, -1) => DirectionType.NorthEast,
            (1, 0) => DirectionType.East,
            (1, 1) => DirectionType.SouthEast,
            (0, 1) => DirectionType.South,
            (-1, 1) => DirectionType.SouthWest,
            (-1, 0) => DirectionType.West,
            (-1, -1) => DirectionType.NorthWest,
            _ => DirectionType.North
        };

    private sealed class ContextScope : IDisposable
    {
        private readonly AiActionService _owner;
        private readonly BrainContext? _previous;

        public ContextScope(AiActionService owner, BrainContext? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        public void Dispose()
        {
            _owner._current = _previous;
        }
    }
}
