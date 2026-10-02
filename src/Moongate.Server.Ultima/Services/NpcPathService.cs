using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Movement;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps one <see cref="NpcPath" /> per walking NPC and searches a new one only when it must: with none left, a
///     changed goal or a blocked step, and two seconds after the last search at the soonest, ten after one that found
///     nothing. Until then a changed goal is walked towards on the old path, as ModernUO.
/// </summary>
public sealed class NpcPathService : INpcPathService
{
    private const long RepathDelayMs = 2000;

    // A search that finds nothing is the costly one, a whole search: it is tried again later than the others.
    private const long FailedDelayMs = 10_000;
    private const long IdleMs = 60_000;

    // Above this many paths, those of NPCs that stopped asking are dropped.
    private const int PruneAbove = 512;

    // A place is reached within a mover's height of it, as the path search counts it.
    private const int MoverHeight = 16;

    private readonly Dictionary<Serial, NpcPath> _paths = [];
    private readonly IPathfindingService _finder;
    private readonly TimeProvider _time;

    public NpcPathService(IPathfindingService finder, TimeProvider time)
    {
        _finder = finder;
        _time = time;
    }

    public NpcPathStep Next(MobileEntity npc, Point3D goal, int range, MovementAbilityType ability)
    {
        if (HasArrived(npc.Location, goal, range))
        {
            _paths.Remove(npc.Id);

            return new(NpcWalkType.Arrived);
        }

        var now = NowMs();

        if (!_paths.TryGetValue(npc.Id, out var path))
        {
            Prune(now);
            path = _paths[npc.Id] = new() { Map = npc.Map, Goal = goal, Expected = npc.Location };
        }

        path.LastUsedAt = now;

        // Moved by something else, such as a teleporter: the steps left start from where it no longer stands.
        if (path.Expected != npc.Location || path.Map != npc.Map)
        {
            path.Steps.Clear();
        }

        if ((path.Steps.Count == 0 || path.Goal != goal) && now >= path.NextSearchAt)
        {
            Search(npc, path, goal, ability, now);
        }

        if (path.Steps.Count > 0)
        {
            return new(NpcWalkType.Moving, path.Steps.Peek());
        }

        return new(path.Failed ? NpcWalkType.NoPath : NpcWalkType.Blocked);
    }

    public void Stepped(MobileEntity npc, bool moved)
    {
        if (!_paths.TryGetValue(npc.Id, out var path))
        {
            return;
        }

        if (moved && path.Steps.Count > 0)
        {
            path.Steps.Dequeue();
            path.Expected = npc.Location;

            return;
        }

        path.Steps.Clear();
        path.Failed = false;
    }

    public void Forget(Serial npc)
    {
        _paths.Remove(npc);
    }

    private static bool HasArrived(Point3D location, Point3D goal, int range)
    {
        return Math.Abs(location.X - goal.X) <= range &&
               Math.Abs(location.Y - goal.Y) <= range &&
               Math.Abs(location.Z - goal.Z) <= MoverHeight;
    }

    private void Search(MobileEntity npc, NpcPath path, Point3D goal, MovementAbilityType ability, long now)
    {
        var found = _finder.FindPath(npc.Map, npc.Location, goal, ability, true);
        path.Map = npc.Map;
        path.Goal = goal;
        path.Expected = npc.Location;
        path.Steps.Clear();

        foreach (var step in found.Steps)
        {
            path.Steps.Enqueue(step);
        }

        path.Failed = path.Steps.Count == 0;
        path.NextSearchAt = now + (path.Failed ? FailedDelayMs : RepathDelayMs);
    }

    private void Prune(long now)
    {
        if (_paths.Count < PruneAbove)
        {
            return;
        }

        foreach (var serial in _paths.Where(pair => now - pair.Value.LastUsedAt > IdleMs).Select(pair => pair.Key).ToList())
        {
            _paths.Remove(serial);
        }
    }

    private long NowMs()
    {
        // Int128: a nanosecond timestamp times 1000 overflows a long after about 106 days of uptime.
        return (long)((Int128)_time.GetTimestamp() * 1000 / _time.TimestampFrequency);
    }
}
