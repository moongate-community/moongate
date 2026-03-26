using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.Server.Services.Interaction;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Modules;

[ScriptModule("perception", "Provides NPC perception helpers by mobile serial.")]

/// <summary>
/// Exposes distance and nearby mobile lookup helpers to Lua brains.
/// </summary>
public sealed class PerceptionModule
{
    private static readonly TimeSpan AggressionTimeout = TimeSpan.FromMinutes(2);
    private readonly IAiRelationService _aiRelationService;
    private readonly ISpatialWorldService _spatialWorldService;

    public PerceptionModule(
        ISpatialWorldService spatialWorldService,
        IAiRelationService? aiRelationService = null
    )
    {
        _aiRelationService = aiRelationService ?? new AiRelationService();
        _spatialWorldService = spatialWorldService;
    }

    [ScriptFunction("distance", "Returns 2D distance between two mobiles, or -1 when unresolved.")]
    public int Distance(uint sourceSerial, uint targetSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, sourceSerial, out var source) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out var target))
        {
            return -1;
        }

        return (int)Math.Round(source!.Location.GetDistance(target!.Location));
    }

    [ScriptFunction("find_nearest_enemy", "Returns nearest enemy mobile serial in range, or nil.")]
    public uint? FindNearestEnemy(uint npcSerial, int range)
    {
        if (range <= 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return null;
        }

        var nearest = FindNearestByPredicate(
            npc!,
            range,
            candidate => candidate.Id != npc!.Id && _aiRelationService.Compute(npc!, candidate) == AiRelation.Hostile
        );

        return nearest is null ? null : (uint)nearest.Id;
    }

    [ScriptFunction("find_nearest_friend", "Returns nearest non-player friend mobile serial in range, or nil.")]
    public uint? FindNearestFriend(uint npcSerial, int range)
    {
        if (range <= 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return null;
        }

        var nearest = FindNearestByPredicate(
            npc!,
            range,
            candidate => candidate.Id != npc!.Id && !candidate.IsPlayer
        );

        return nearest is null ? null : (uint)nearest.Id;
    }

    [ScriptFunction("find_nearest_player_enemy", "Returns nearest player-controlled enemy mobile serial in range, or nil.")]
    public uint? FindNearestPlayerEnemy(uint npcSerial, int range)
    {
        if (range <= 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return null;
        }

        var nearest = FindNearestByPredicate(
            npc!,
            range,
            candidate => candidate.Id != npc!.Id &&
                         candidate.IsPlayer &&
                         _aiRelationService.Compute(npc!, candidate) == AiRelation.Hostile
        );

        return nearest is null ? null : (uint)nearest.Id;
    }

    [ScriptFunction("find_best_target", "Returns the best hostile target serial for the provided fight mode, or nil.")]
    public uint? FindBestTarget(uint npcSerial, int range, string fightMode, bool playersOnly = false)
    {
        if (range <= 0 ||
            ResolveFightMode(fightMode) is not { } resolvedFightMode ||
            resolvedFightMode == FightMode.None ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return null;
        }

        UOMobileEntity? bestTarget = null;
        var bestDistanceSquared = int.MaxValue;

        foreach (var candidate in _spatialWorldService.GetNearbyMobiles(npc!.Location, range, npc.MapId))
        {
            if (!IsEligibleTarget(npc, candidate, resolvedFightMode, playersOnly))
            {
                continue;
            }

            var distanceSquared = GetDistanceSquared(npc, candidate);

            if (bestTarget is null || ShouldReplaceBestTarget(candidate, distanceSquared, bestTarget, bestDistanceSquared, resolvedFightMode))
            {
                bestTarget = candidate;
                bestDistanceSquared = distanceSquared;
            }
        }

        return bestTarget is null ? null : (uint)bestTarget.Id;
    }

    [ScriptFunction("in_range", "Returns true when two mobiles are within tile range.")]
    public bool InRange(uint sourceSerial, uint targetSerial, int range)
    {
        if (range < 0 ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, sourceSerial, out var source) ||
            !MobileScriptResolver.TryResolveMobile(_spatialWorldService, targetSerial, out var target))
        {
            return false;
        }

        return source!.Location.InRange(target!.Location, range);
    }

    private UOMobileEntity? FindNearestByPredicate(
        UOMobileEntity source,
        int range,
        Func<UOMobileEntity, bool> predicate
    )
    {
        UOMobileEntity? nearest = null;
        var nearestDistanceSquared = int.MaxValue;

        foreach (var candidate in _spatialWorldService.GetNearbyMobiles(source.Location, range, source.MapId))
        {
            if (!predicate(candidate))
            {
                continue;
            }

            var dx = source.Location.X - candidate.Location.X;
            var dy = source.Location.Y - candidate.Location.Y;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearest = candidate;
        }

        return nearest;
    }

    private bool IsEligibleTarget(UOMobileEntity source, UOMobileEntity candidate, FightMode fightMode, bool playersOnly)
    {
        if (candidate.Id == source.Id || (playersOnly && !candidate.IsPlayer))
        {
            return false;
        }

        return fightMode switch
        {
            FightMode.Aggressor => IsHostile(source, candidate) && HasRecentAggression(source, candidate),
            FightMode.Evil => IsHostile(source, candidate) || IsEnemyStyleTarget(candidate),
            FightMode.Closest => IsHostile(source, candidate),
            FightMode.Weakest => IsHostile(source, candidate),
            FightMode.Strongest => IsHostile(source, candidate),
            _ => false
        };
    }

    private bool IsHostile(UOMobileEntity source, UOMobileEntity candidate)
        => _aiRelationService.Compute(source, candidate) == AiRelation.Hostile;

    private static bool IsEnemyStyleTarget(UOMobileEntity candidate)
        => candidate.Karma < 0 ||
           candidate.Notoriety is Notoriety.CanBeAttacked or
                                Notoriety.Enemy or
                                Notoriety.Criminal or
                                Notoriety.Murdered;

    private static int GetDistanceSquared(UOMobileEntity source, UOMobileEntity candidate)
    {
        var dx = source.Location.X - candidate.Location.X;
        var dy = source.Location.Y - candidate.Location.Y;

        return dx * dx + dy * dy;
    }

    private static double GetStrengthTacticsScore(UOMobileEntity mobile)
        => (mobile.GetSkill(UOSkillName.Tactics)?.Value ?? 0.0) + mobile.Strength;

    private static bool HasRecentAggression(UOMobileEntity source, UOMobileEntity target)
    {
        var nowUtc = DateTime.UtcNow;

        return source.Aggressors.Any(entry => MatchesRecentAggression(entry, target.Id, nowUtc)) ||
               source.Aggressed.Any(entry => MatchesRecentAggression(entry, target.Id, nowUtc)) ||
               target.Aggressors.Any(entry => MatchesRecentAggression(entry, source.Id, nowUtc)) ||
               target.Aggressed.Any(entry => MatchesRecentAggression(entry, source.Id, nowUtc));
    }

    private static bool MatchesRecentAggression(AggressorInfo entry, Serial targetId, DateTime nowUtc)
        => (entry.AttackerId == targetId || entry.DefenderId == targetId) &&
           nowUtc - entry.LastCombatAtUtc <= AggressionTimeout;

    private static bool ShouldReplaceBestTarget(
        UOMobileEntity candidate,
        int candidateDistanceSquared,
        UOMobileEntity bestTarget,
        int bestDistanceSquared,
        FightMode fightMode
    )
    {
        switch (fightMode)
        {
            case FightMode.Weakest:
                if (candidate.Hits != bestTarget.Hits)
                {
                    return candidate.Hits < bestTarget.Hits;
                }

                break;
            case FightMode.Strongest:
                var candidateScore = GetStrengthTacticsScore(candidate);
                var bestScore = GetStrengthTacticsScore(bestTarget);

                if (!candidateScore.Equals(bestScore))
                {
                    return candidateScore > bestScore;
                }

                break;
        }

        if (candidateDistanceSquared != bestDistanceSquared)
        {
            return candidateDistanceSquared < bestDistanceSquared;
        }

        return candidate.Id < bestTarget.Id;
    }

    private static FightMode? ResolveFightMode(string fightMode)
    {
        if (string.IsNullOrWhiteSpace(fightMode))
        {
            return null;
        }

        if (string.Equals(fightMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return FightMode.None;
        }

        if (string.Equals(fightMode, "aggressor", StringComparison.OrdinalIgnoreCase))
        {
            return FightMode.Aggressor;
        }

        if (string.Equals(fightMode, "closest", StringComparison.OrdinalIgnoreCase))
        {
            return FightMode.Closest;
        }

        if (string.Equals(fightMode, "weakest", StringComparison.OrdinalIgnoreCase))
        {
            return FightMode.Weakest;
        }

        if (string.Equals(fightMode, "strongest", StringComparison.OrdinalIgnoreCase))
        {
            return FightMode.Strongest;
        }

        if (string.Equals(fightMode, "evil", StringComparison.OrdinalIgnoreCase))
        {
            return FightMode.Evil;
        }

        return null;
    }

    private enum FightMode
    {
        None,
        Aggressor,
        Closest,
        Weakest,
        Strongest,
        Evil
    }
}
