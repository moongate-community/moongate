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
using Moongate.Server.Abstractions.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using Serilog;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using ILogger = Serilog.ILogger;

namespace Moongate.Server.Services.AI;

/// <summary>Validates and applies the bounded world effects represented by NPC brain intents.</summary>
public sealed class BrainIntentExecutor : IBrainIntentExecutor
{
    private const int SpeechRange = 15;
    private const int SpeechMaximumLength = 128;
    private const int HomeLeashRange = 8;

    private readonly ILogger _logger = Log.ForContext<BrainIntentExecutor>();
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;
    private readonly IMovementService _movement;
    private readonly IChatService _chat;
    private readonly Random _random;
    private readonly ILoopAffinity _loopAffinity;
    private readonly INpcAiMetrics _metrics;

    public BrainIntentExecutor(
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

    public void Execute(Serial mobileId, BrainContext context, IReadOnlyList<BrainIntent> intents)
    {
        _loopAffinity.AssertOnLoop("ai.brain_intent_execute");

        foreach (var intent in intents ?? [])
        {
            var owner = _mobiles.GetById(mobileId);

            if (owner is null)
            {
                Reject(mobileId, intent, "owner is missing");
                continue;
            }

            if (context is null)
            {
                Reject(mobileId, intent, "brain context is missing");
                continue;
            }

            if (TryExecute(owner, context, intent, out var reason))
            {
                _metrics.RecordIntent(true);
                continue;
            }

            Reject(mobileId, intent, reason);
        }
    }

    private bool TryExecute(MobileEntity owner, BrainContext context, BrainIntent? intent, out string reason)
    {
        if (intent is null)
        {
            reason = "intent is missing";
            return false;
        }

        switch (intent.Type)
        {
            case BrainIntentType.Idle:
                reason = "";
                return true;
            case BrainIntentType.Say:
                return TrySay(owner, intent.Text, out reason);
            case BrainIntentType.Patrol:
                return TryPatrol(owner, context, out reason);
            case BrainIntentType.MoveToward:
                return TryMove(owner, context, intent.TargetId, false, out reason);
            case BrainIntentType.MoveAway:
                return TryMove(owner, context, intent.TargetId, true, out reason);
            case BrainIntentType.Engage:
                return TryEngage(owner, context, intent.TargetId, out reason);
            case BrainIntentType.ClearTarget:
                return TryClearTarget(owner, out reason);
            case BrainIntentType.ReturnHome:
                return TryReturnHome(owner, context, out reason);
            default:
                reason = "intent type is unknown or unsupported";
                return false;
        }
    }

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

    private void Reject(Serial mobileId, BrainIntent? intent, string reason)
    {
        _logger.Debug(
            "Rejected NPC brain intent {MobileId} {RawType}: {Reason}",
            mobileId,
            intent?.RawType,
            reason
        );
        _metrics.RecordIntent(false);
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
}
