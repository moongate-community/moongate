using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Movement;
using Moongate.Server.Ultima.Data.Movement;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps one <see cref="NpcPath" /> per walking NPC and searches a new one only when it must: with no steps left or a
///     changed goal, two seconds after the NPC's last search at the soonest, ten when that search did not reach the same
///     goal, and for a few NPCs a second in the whole server. An NPC that may not search steps straight towards its
///     goal, as ModernUO, so one that chases a moving target keeps moving.
/// </summary>
public sealed class NpcPathService : INpcPathService
{
    /// <summary>
    ///     How many searches run in a second for all the NPCs together: a search that finds nothing takes about 12 ms.
    /// </summary>
    public const int SearchesPerSecond = 10;

    private const long RepathDelayMs = 2000;

    // A search that does not reach its goal is the costly one, a whole search: the same goal is tried again later.
    private const long UnreachedDelayMs = 10_000;
    private const long IdleMs = 60_000;
    private const long BudgetWindowMs = 1000;

    // Above this many paths, those of NPCs that stopped asking are dropped.
    private const int PruneAbove = 512;

    // A place is reached within a mover's height of it, as the path search counts it.
    private const int MoverHeight = 16;

    private readonly Dictionary<Serial, NpcPath> _paths = [];
    private readonly IPathfindingService _finder;
    private readonly TimeProvider _time;
    private long _windowStartedAt = long.MinValue;
    private int _searchesInWindow;

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
        var moved = path.Expected != npc.Location || path.Map != npc.Map;

        if (moved)
        {
            path.Steps.Clear();
            path.Expected = npc.Location;
            path.Map = npc.Map;
        }

        // What the last search said holds only for its goal, and only from where the NPC then was.
        var sameSearch = path.Searched && path.Goal == goal && !moved;

        if (moved)
        {
            path.Unreached = false;
        }

        if ((path.Steps.Count == 0 || path.Goal != goal) && MaySearch(path, sameSearch, now))
        {
            Search(npc, path, goal, ability, now);
            sameSearch = true;
        }

        if (path.Steps.Count > 0)
        {
            return new(NpcWalkType.Moving, path.Steps.Peek());
        }

        // The last search of this very goal led nowhere, or a straight step was just refused: wait.
        if (sameSearch && path.Unreached)
        {
            return new(NpcWalkType.NoPath);
        }

        if (path.StraightRefused)
        {
            return new(NpcWalkType.Blocked);
        }

        // No path it may follow or search yet: straight towards the goal, as ModernUO's follower.
        path.Straight = true;

        return new(NpcWalkType.Moving, npc.Location.GetDirectionTo(new Point3D(goal.X, goal.Y, npc.Location.Z)));
    }

    public void Stepped(MobileEntity npc, bool moved)
    {
        if (!_paths.TryGetValue(npc.Id, out var path))
        {
            return;
        }

        var straight = path.Straight;
        path.Straight = false;

        if (moved)
        {
            if (!straight && path.Steps.Count > 0)
            {
                path.Steps.Dequeue();
            }

            path.Expected = npc.Location;
            path.StraightRefused = false;

            return;
        }

        path.Steps.Clear();
        path.StraightRefused = true;
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

    private bool MaySearch(NpcPath path, bool sameSearch, long now)
    {
        if (path.Searched && now < path.SearchedAt + (sameSearch && path.Unreached ? UnreachedDelayMs : RepathDelayMs))
        {
            return false;
        }

        if (now - _windowStartedAt >= BudgetWindowMs || _windowStartedAt == long.MinValue)
        {
            _windowStartedAt = now;
            _searchesInWindow = 0;
        }

        return _searchesInWindow < SearchesPerSecond;
    }

    private void Search(MobileEntity npc, NpcPath path, Point3D goal, MovementAbilityType ability, long now)
    {
        _searchesInWindow++;
        var found = _finder.FindPath(npc.Map, npc.Location, goal, ability, true);
        path.Map = npc.Map;
        path.Goal = goal;
        path.Expected = npc.Location;
        path.Searched = true;
        path.SearchedAt = now;
        path.Unreached = found.Kind != PathResultType.Found;
        path.Straight = false;
        path.StraightRefused = false;
        path.Steps.Clear();

        foreach (var step in found.Steps)
        {
            path.Steps.Enqueue(step);
        }
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
