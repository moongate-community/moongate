using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Modules;

[ScriptModule("perception", "Provides NPC perception helpers by mobile serial.")]

/// <summary>
/// Exposes distance and nearby mobile lookup helpers to Lua brains.
/// </summary>
public sealed class PerceptionModule
{
    private readonly IAiRelationService _aiRelationService;
    private readonly ISpatialWorldService _spatialWorldService;

    public PerceptionModule(
        ISpatialWorldService spatialWorldService,
        IAiRelationService? aiRelationService = null
    )
    {
        _aiRelationService = aiRelationService ?? new Services.Interaction.AiRelationService();
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
}
