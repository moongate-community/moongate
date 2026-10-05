using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Core.Geometry;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;
using Serilog;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules.Internal;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Movement;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>npc</c> Lua module: a mobile script acts on its NPC by serial, as <c>npc.say(serial, "Hail")</c>. A serial
///     that is not an NPC in the world gives <c>false</c> or <c>nil</c>, never an error: a script that waited may outlive
///     its NPC, and a script must never move or voice a player.
/// </summary>
[ScriptModule("npc", "Acts as an NPC: speaks, plays sounds, walks and reads where it is.")]
public sealed class NpcModule
{
    public const int MaximumTextLength = 128;

    private const DirectionType DirectionMask = (DirectionType)0x07;

    // A mover's height: the ground of a place it walks to is first looked for no higher than its head.
    private const int MoverHeight = 16;

    // The eye of a mobile above its feet, as the reach of an item is checked.
    private const int EyeHeight = 14;

    // How far an NPC sees when a script names no range, ModernUO's perception.
    private const int DefaultSight = 16;

    private const string HomeX1 = "spawn.x1";
    private const string HomeY1 = "spawn.y1";
    private const string HomeX2 = "spawn.x2";
    private const string HomeY2 = "spawn.y2";

    private const int Directions = 8;

    private readonly IMobileService _mobiles;
    private readonly ISpeechService _speech;
    private readonly IWorldViewService _view;
    private readonly IMobileTemplateService _templates;
    private readonly INpcService? _npcs;
    private readonly Lazy<IScriptEngine>? _engine;
    private readonly IGameLoopService? _loop;
    private readonly ISectorService? _sectors;
    private readonly IMoveOverService? _moveOver;
    private readonly INpcPathService? _paths;
    private readonly IPathfindingService? _finder;
    private readonly IMovementService? _movement;
    private readonly ISessionService? _sessions;
    private readonly ILineOfSightService? _sight;
    private readonly ILogger _logger = Log.ForContext<NpcModule>();

    public NpcModule(
        IMobileService mobiles,
        ISpeechService speech,
        IWorldViewService view,
        IMobileTemplateService templates,
        INpcService? npcs = null,
        Lazy<IScriptEngine>? engine = null,
        IGameLoopService? loop = null,
        ISectorService? sectors = null,
        IMoveOverService? moveOver = null,
        INpcPathService? paths = null,
        IPathfindingService? finder = null,
        IMovementService? movement = null,
        ISessionService? sessions = null,
        ILineOfSightService? sight = null
    )
    {
        _sessions = sessions;
        _sight = sight;
        _paths = paths;
        _finder = finder;
        _movement = movement;
        _moveOver = moveOver;
        _npcs = npcs;
        _engine = engine;
        _loop = loop;
        _sectors = sectors;
        _mobiles = mobiles;
        _speech = speech;
        _view = view;
        _templates = templates;
    }

    /// <summary>
    ///     Makes the NPC say <paramref name="text" /> overhead to the players within 15 cells; <c>npc.say(serial, text)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The NPC says text overhead (cut to 128 characters) to the players within 15 cells; false for an unknown NPC or blank text.")]
    public bool Say(long serial, string text)
    {
        if (!TryGetNpc(serial, out var npc) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        _speech.Say(npc, text.Length > MaximumTextLength ? text[..MaximumTextLength] : text);

        return true;
    }

    /// <summary>
    ///     Plays a sound where the NPC stands for the players within 15 cells: a sound id, <c>npc.play_sound(serial, 0x69)</c>,
    ///     or a kind of the NPC template's <c>[mobile.sounds]</c>, <c>npc.play_sound(serial, "idle")</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays a sound id (0 to 65535) or a kind of its template's [mobile.sounds] (start_attack, idle, attack, hurt, death) where the NPC stands, for the players within 15 cells (0x54); false for an unknown NPC, a sound out of range or a kind its template does not set.")]
    public bool PlaySound(long serial, object sound)
    {
        if (!TryGetNpc(serial, out var npc) || ResolveSound(npc, sound) is not { } id)
        {
            return false;
        }

        _speech.PlaySound(npc, id);

        return true;
    }

    /// <summary>
    ///     Turns the NPC toward <paramref name="direction" /> when needed and takes one step, a run when
    ///     <paramref name="running" />; <c>npc.step(serial, DirectionType.North, true)</c>. The players in range see the
    ///     turn and the step. <c>DirectionType.Running</c> is not a direction: pass <paramref name="running" /> instead.
    /// </summary>
    [ScriptFunction(helpText: "One step in a direction (North to NorthWest), a run when running is true, turning first when needed, seen by the players in range; how often it is called sets the speed. False when blocked or for DirectionType.Running, which is not a direction.")]
    public bool Step(long serial, DirectionType direction, bool running = false)
    {
        if ((direction & ~DirectionMask) != 0 || !TryGetNpc(serial, out var npc))
        {
            return false;
        }

        return Take(npc, direction, running);
    }

    /// <summary>
    ///     Takes one step towards a place along a path that avoids what stands in the way, and says how it goes; call it
    ///     from <c>on_think</c> on every tick: <c>if npc.walk_to(serial, 1434, 1699) == "arrived" then ... end</c>. The
    ///     path is searched the first time and kept: it is searched again only when the place changes or a step is
    ///     blocked, and two seconds after the last search at the soonest.
    /// </summary>
    [ScriptFunction(helpText: "One step along a path to x, y (z defaults to the ground there), a run when running is true: 'arrived' within range tiles of it (default 0), 'moving' after a step, 'blocked' when the step was refused or it waits to look for another way, 'no_path' when the last search did not reach the place; nil for an unknown NPC, a negative range or a z outside -128 to 127.")]
    public string? WalkTo(long serial, int x, int y, int? z = null, int? range = null, bool running = false)
    {
        if (_paths is null || range is < 0 || !TryGetNpc(serial, out var npc) || GoalOf(npc, x, y, z) is not { } goal)
        {
            return null;
        }

        var step = _paths.Next(npc, goal, range ?? 0, AbilityOf(npc));

        switch (step.Kind)
        {
            case NpcWalkType.Arrived:
                return "arrived";
            case NpcWalkType.NoPath:
                return "no_path";
            case NpcWalkType.Blocked:
                return "blocked";
        }

        var moved = Take(npc, step.Direction, running);
        _paths.Stepped(npc, moved);

        return moved ? "moving" : "blocked";
    }

    /// <summary>
    ///     Finds the steps from the NPC to a place, for a script that walks them itself with <c>npc.step</c>;
    ///     <c>for _, direction in ipairs(npc.find_path(serial, 1434, 1699) or {}) do ... end</c>. Each call searches:
    ///     keep the list, do not ask on every tick.
    /// </summary>
    [ScriptFunction(helpText: "The steps from the NPC to x, y (z defaults to the ground there) as a list of DirectionType, to walk with npc.step; with partial true, the steps to the closest place when it cannot be reached. Each call searches, so keep the list. Nil when there is no path, the place is too far or the NPC is unknown.")]
    public LuaTable? FindPath(long serial, int x, int y, int? z = null, bool partial = false)
    {
        if (_finder is null || !TryGetNpc(serial, out var npc) || GoalOf(npc, x, y, z) is not { } goal)
        {
            return null;
        }

        var path = _finder.FindPath(npc.Map, npc.Location, goal, AbilityOf(npc), partial);

        if (path.Kind is not (PathResultType.Found or PathResultType.Partial))
        {
            return null;
        }

        var table = new LuaTable();
        var index = 1;

        foreach (var direction in path.Steps)
        {
            table[index++] = (int)direction;
        }

        return table;
    }

    // One step, a turn first when needed, shown to the players around; true when the NPC moved.
    private bool Take(MobileEntity npc, DirectionType direction, bool running)
    {
        var oldLocation = npc.Location;
        var oldDirection = npc.Direction;
        var ability = AbilityOf(npc);
        var result = _mobiles.TryMove(npc, direction, ability);

        if (result == MoveResultType.Turned)
        {
            result = _mobiles.TryMove(npc, direction, ability);
        }

        if (npc.Location != oldLocation || npc.Direction != oldDirection)
        {
            _view.Moved(npc, oldLocation, running);
        }

        // On the next turn of the loop: the items' scripts cannot run inside the NPC's own, which called this.
        // Only for a cell that holds an item: most steps post nothing.
        if (npc.Location != oldLocation &&
            _moveOver is not null &&
            _sectors?.GetItemsInRange(npc.Map, npc.Location, 0).Count > 0)
        {
            _loop?.TryPost(new LoopActionWorkItem(() => _moveOver.SteppedOn(npc)));
        }

        return result == MoveResultType.Moved;
    }

    /// <summary>
    ///     Brings a new NPC of a template into the world; <c>npc.spawn("orc", MapType.Trammel, 1500, 1600, 10,
    ///     function(serial) npc.say(serial, "Grr") end)</c>. The NPC is saved first, so it appears a moment later: its
    ///     script's <c>on_spawn</c> runs then, and so does <paramref name="callback" />, with its serial.
    /// </summary>
    [ScriptFunction(helpText: "Spawns an NPC of a mobile template at x, y, z of the map. It is saved first, so it appears a moment later: its on_spawn runs then, and so does the optional function with its serial. False for an unknown template, a spot outside the map or a z outside -128 to 127.")]
    public bool Spawn(
        string template,
        MapType map,
        int x,
        int y,
        int z,
        [ScriptParameterType("function")] LuaValue callback = default
    )
    {
        if (callback.Type is not (LuaValueType.Nil or LuaValueType.Function))
        {
            throw new ArgumentException($"expected a function, got {callback.TypeToString()}", nameof(callback));
        }

        if (_npcs is null ||
            z is < sbyte.MinValue or > sbyte.MaxValue ||
            string.IsNullOrWhiteSpace(template) ||
            !_templates.TryGet(template, out _) ||
            _sectors?.IsInside(map, x, y) == false)
        {
            return false;
        }

        var function = callback.Type == LuaValueType.Function ? callback.Read<LuaFunction>() : null;
        var owner = _engine?.Value.CurrentScript ?? "npc.spawn";
        _ = SpawnAsync(template, map, new Point3D(x, y, z), function, owner);

        return true;
    }

    /// <summary>
    ///     Takes the NPC out of the world for good, with what it carries; <c>npc.delete(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Deletes the NPC and what it carries, on the next turn of the game loop; false for a serial that is not an NPC in the world.")]
    public bool Delete(long serial)
    {
        if (_npcs is null || !TryGetNpc(serial, out var npc))
        {
            return false;
        }

        // The removal posts to the game loop and waits: a script runs on that loop, so it is started off it.
        var id = npc.Id;
        _ = Task.Run(() => _npcs.RemoveAsync(id))
                .ContinueWith(
                    task => _logger.Warning(task.Exception, "npc.delete of {Serial} failed", serial),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default
                );

        return true;
    }

    /// <summary>
    ///     Turns the NPC towards a place without stepping; <c>npc.face(serial, there.x, there.y)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Turns the NPC towards x, y without stepping, seen by the players in range; false for an unknown or frozen NPC or its own cell.")]
    public bool Face(long serial, int x, int y)
    {
        if (!TryGetNpc(serial, out var npc) || npc.Frozen || npc.Location.X == x && npc.Location.Y == y)
        {
            return false;
        }

        var direction = npc.Location.GetDirectionTo(new Point3D(x, y, npc.Location.Z)) & DirectionMask;

        if (direction != npc.Direction)
        {
            npc.Direction = direction;
            _view.Moved(npc, npc.Location, false);
        }

        return true;
    }

    /// <summary>
    ///     Turns the NPC towards a player or another NPC without stepping, as towards who speaks to it;
    ///     <c>npc.look_at(serial, speaker)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Turns the NPC towards another mobile, player or NPC, without stepping, as npc.face does towards a place: towards who speaks to it. False for an unknown or frozen NPC, a mobile not in the world or on another map, or one on its own cell.")]
    public bool LookAt(long serial, long other)
    {
        return TryGetNpc(serial, out var npc) &&
               other is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)other), out var mobile) &&
               _mobiles.IsInWorld(mobile.Id) &&
               mobile.Map == npc.Map &&
               Face(serial, mobile.Location.X, mobile.Location.Y);
    }

    /// <summary>
    ///     Gets how many tiles lie between the NPC and a place, as the view range counts them;
    ///     <c>npc.distance_to(serial, there.x, there.y) <= 2</c>.
    /// </summary>
    [ScriptFunction(helpText: "The tiles between the NPC and x, y, the larger of the two differences, as the view range counts them; nil for an unknown NPC.")]
    public int? DistanceTo(long serial, int x, int y)
    {
        return TryGetNpc(serial, out var npc) ? Distance(npc.Location, new Point3D(x, y, 0)) : null;
    }

    /// <summary>
    ///     Gets the mobiles around the NPC, itself left out, nearest first, as a list of serials: the other NPCs, or
    ///     with <paramref name="kind" /> the players or everyone; <c>for _, other in ipairs(npc.nearby(serial, 8)) do ...
    ///     end</c>, <c>npc.nearby(serial, 8, "players")</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serials of the mobiles within range tiles (0 to 32) of the NPC, itself left out, nearest first: the other NPCs, or with kind 'players' the players, with 'all' everyone. Height and line of sight are not checked. Empty for an unknown NPC or a range out of bounds.")]
    public LuaTable Nearby(long serial, int range, string kind = "npcs")
    {
        if (kind is not ("npcs" or "players" or "all"))
        {
            throw new ArgumentException($"expected 'npcs', 'players' or 'all', got '{kind}'", nameof(kind));
        }

        var table = new LuaTable();

        if (_sectors is null || range is < 0 or > WorldModule.MaximumRange || !TryGetNpc(serial, out var npc))
        {
            return table;
        }

        var index = 1;

        foreach (var other in _sectors.GetMobilesInRange(npc.Map, npc.Location, range)
                                      .Where(other => other.Id != npc.Id && (kind == "all" || other.IsNpc == (kind == "npcs")))
                                      .OrderBy(other => Distance(npc.Location, other.Location))
                                      .ThenBy(other => other.Id.Value))
        {
            table[index++] = (long)other.Id.Value;
        }

        return table;
    }

    /// <summary>
    ///     Gets the home of an NPC of a spawn region, the area it was spawned in, as <c>{ x1, y1, x2, y2 }</c>;
    ///     <c>npc.home(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The home of an NPC of a spawn region, the area it was spawned in, as a table { x1, y1, x2, y2 }; nil for an NPC without one or an unknown NPC.")]
    public LuaTable? Home(long serial)
    {
        if (!TryGetNpc(serial, out var npc) || HomeOf(npc) is not { } home)
        {
            return null;
        }

        var table = new LuaTable();
        table["x1"] = home.Start.X;
        table["y1"] = home.Start.Y;
        table["x2"] = home.End.X;
        table["y2"] = home.End.Y;

        return table;
    }

    /// <summary>
    ///     One stroll step, as ModernUO's wander: mostly straight ahead, now and then another way; <c>npc.wander(serial)</c>
    ///     on a think. An NPC with a home keeps to it, and from outside it walks back, around what stands in the way.
    /// </summary>
    [ScriptFunction(helpText: "One stroll step, as ModernUO's wander: two times in three straight ahead, else another way. An NPC of a spawn region keeps to its home and from outside walks back along a path, with a random step when none is found. False when it did not move, as in a home of one cell, or for an unknown NPC.")]
    public bool Wander(long serial)
    {
        if (!TryGetNpc(serial, out var npc))
        {
            return false;
        }

        var facing = (int)(npc.Direction & DirectionMask);
        // One time in three it turns somewhere else.
        var first = RandomUtils.Random(3) == 0 ? RandomUtils.Random(Directions) : facing;

        if (HomeOf(npc) is not { } home)
        {
            return Take(npc, (DirectionType)first, false);
        }

        var here = npc.Location;

        // Outside, as after a chase: back to the nearest cell of home along a path. With no way found it steps at
        // random, so a wall between it and home does not hold it for ever; without a path service it goes straight.
        if (!Contains(home, here.X, here.Y))
        {
            var x = Math.Clamp(here.X, home.Start.X, home.End.X);
            var y = Math.Clamp(here.Y, home.Start.Y, home.End.Y);
            var state = WalkTo(serial, x, y);

            if (state == "moving")
            {
                return true;
            }

            var direction = state is null
                                ? here.GetDirectionTo(new Point3D(x, y, here.Z)) & DirectionMask
                                : (DirectionType)RandomUtils.Random(Directions);

            return Take(npc, direction, false);
        }

        for (var turn = 0; turn < Directions; turn++)
        {
            var direction = (DirectionType)((first + turn) % Directions);
            var (dx, dy) = Offset(direction);

            if (Contains(home, here.X + dx, here.Y + dy))
            {
                return Take(npc, direction, false);
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets whether the NPC sees a mobile: in the world, on its map, within <paramref name="range" /> tiles, not
    ///     hidden, not staff and in line of sight from eye to eye; <c>npc.can_see(serial, user)</c>. With
    ///     <paramref name="inSight" /> false the line of sight is not checked: what an NPC keeps following once it saw it.
    /// </summary>
    [ScriptFunction(helpText: "Whether the NPC sees the mobile: on its map, within range tiles (default 16, 0 to 32), not hidden, not a game master or an administrator, and in line of sight from eye to eye (never beyond ultima.line_of_sight.max_distance, 25); with in_sight false the line of sight is not checked. False for itself or an unknown NPC or mobile.")]
    public bool CanSee(long serial, long other, int range = DefaultSight, bool inSight = true)
    {
        return range is >= 0 and <= WorldModule.MaximumRange &&
               other is > 0 and <= uint.MaxValue &&
               TryGetNpc(serial, out var npc) &&
               _mobiles.TryGet(new Serial((uint)other), out var mobile) &&
               Sees(npc, mobile, range, inSight);
    }

    /// <summary>
    ///     Gets the players the NPC sees, nearest first, as a list of serials; <c>for _, player in
    ///     ipairs(npc.players_in_sight(serial, 16)) do ... end</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serials of the players the NPC sees within range tiles (default 16, 0 to 32), as npc.can_see says, nearest first; with limit, that many at most, so a script that wants the nearest does not pay for a crowd. A line of sight is not checked beyond ultima.line_of_sight.max_distance (25). Empty for an unknown NPC, a range out of bounds or a limit below 1.")]
    public LuaTable PlayersInSight(long serial, int range = DefaultSight, int limit = int.MaxValue)
    {
        var table = new LuaTable();

        if (_sectors is null || limit < 1 || range is < 0 or > WorldModule.MaximumRange || !TryGetNpc(serial, out var npc))
        {
            return table;
        }

        var index = 1;

        // Nearest first, so the line of sight of a far player is not checked before that of a near one.
        foreach (var player in _sectors.GetMobilesInRange(npc.Map, npc.Location, range)
                                       .Where(other => !other.IsNpc)
                                       .OrderBy(other => Distance(npc.Location, other.Location))
                                       .ThenBy(other => other.Id.Value))
        {
            if (Sees(npc, player, range, true))
            {
                table[index++] = (long)player.Id.Value;

                if (index > limit)
                {
                    break;
                }
            }
        }

        return table;
    }

    /// <summary>
    ///     Gets where the NPC is as <c>{ x, y, z, map }</c>; <c>npc.location(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Where the NPC is, as a table { x, y, z, map }; nil for an unknown NPC.")]
    public LuaTable? Location(long serial)
    {
        if (!TryGetNpc(serial, out var npc))
        {
            return null;
        }

        var table = new LuaTable();
        table["x"] = npc.Location.X;
        table["y"] = npc.Location.Y;
        table["z"] = npc.Location.Z;
        table["map"] = (int)npc.Map;

        return table;
    }

    /// <summary>
    ///     Gets the NPC's name; <c>npc.name(serial)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The NPC's name; nil for an unknown NPC.")]
    public string? Name(long serial)
    {
        return TryGetNpc(serial, out var npc) ? npc.Name : null;
    }

    // A whole number in the sound range, or a kind the template sets, as UOX3's creature sounds.
    private int? ResolveSound(MobileEntity npc, object sound)
    {
        if (sound is double number)
        {
            return number is >= 0 and <= ushort.MaxValue && Math.Floor(number) == number ? (int)number : null;
        }

        if (sound is not string kind ||
            npc.TemplateId is not { } templateId ||
            !_templates.TryGet(templateId, out var template) ||
            template.Sounds is not { } sounds)
        {
            return null;
        }

        return kind switch
        {
            "start_attack" => sounds.StartAttack,
            "idle" => sounds.Idle,
            "attack" => sounds.Attack,
            "hurt" => sounds.Hurt,
            "death" => sounds.Death,
            _ => null
        };
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> the NPC keeps, saved with it across restarts;
    ///     <c>npc.get_prop(serial, "vega.greeted")</c>.
    /// </summary>
    [ScriptFunction(helpText: "A value the NPC keeps across restarts: a string, a number or a bool; nil when it has none.")]
    public object? GetProp(long serial, string key)
    {
        return TryGetNpc(serial, out var npc) && npc.Props is { } props && props.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    ///     Keeps <paramref name="value" /> as the prop <paramref name="key" /> of the NPC, saved with it by the world save,
    ///     or removes it for <c>nil</c>; <c>npc.set_prop(serial, "vega.greeted", 3)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Keeps a string, a number or a bool on the NPC across restarts, saved with it by the world save; nil removes it. False for a table, a function or a blank key.")]
    public bool SetProp(long serial, string key, object? value = null)
    {
        if (string.IsNullOrWhiteSpace(key) || !TryGetNpc(serial, out var npc))
        {
            return false;
        }

        if (value is null)
        {
            npc.RemoveProp(key);

            return true;
        }

        if (!ScriptPropValue.TryFromLua(value, out var prop))
        {
            return false;
        }

        npc.SetProp(key, prop);

        return true;
    }

    // The place a script names: at the height it gives, else on the ground of the cell, of the NPC's own storey when
    // there is one.
    private Point3D? GoalOf(MobileEntity npc, int x, int y, int? z)
    {
        if (z is { } given)
        {
            return given is >= sbyte.MinValue and <= sbyte.MaxValue ? new Point3D(x, y, given) : null;
        }

        var ground = npc.Location.Z;

        try
        {
            // The NPC's own storey first: the highest ground not above its head. Else the highest there, as up a hill.
            if (_movement is not null &&
                (_movement.TryGetSpawnZ(npc.Map, x, y, npc.Location.Z + MoverHeight, out var found) ||
                 _movement.TryGetSpawnZ(npc.Map, x, y, sbyte.MaxValue, out found)))
            {
                ground = found;
            }
        }
        catch (KeyNotFoundException)
        {
            // The map is not loaded: the search will find nothing.
        }

        return new Point3D(x, y, ground);
    }

    private async Task SpawnAsync(string template, MapType map, Point3D location, LuaFunction? callback, string owner)
    {
        try
        {
            var npc = await _npcs!.SpawnAsync(template, map, location);

            if (callback is not null && _engine is not null && _loop is not null)
            {
                _loop.TryPost(new LoopActionWorkItem(() => _engine.Value.CallFunction(owner, callback, (long)npc.Id.Value)));
            }
        }
        catch (Exception exception)
        {
            _logger.Warning(exception, "npc.spawn of {Template} at {Map} {Location} failed", template, map, location);
        }
    }

    // The tiles between two places, as the view range counts them.
    private static int Distance(Point3D from, Point3D to)
    {
        return Math.Max(Math.Abs(from.X - to.X), Math.Abs(from.Y - to.Y));
    }

    private bool Sees(MobileEntity npc, MobileEntity other, int range, bool inSight)
    {
        if (other.Id == npc.Id ||
            other.Hidden ||
            other.Map != npc.Map ||
            !_mobiles.IsInWorld(other.Id) ||
            Distance(npc.Location, other.Location) > range ||
            IsStaff(other))
        {
            return false;
        }

        if (!inSight)
        {
            return true;
        }

        try
        {
            return _sight is not null && _sight.HasLineOfSight(npc.Map, EyeOf(npc), EyeOf(other));
        }
        catch (KeyNotFoundException)
        {
            // The map is not loaded.
            return false;
        }
    }

    // As ModernUO, a monster never goes for the staff.
    private bool IsStaff(MobileEntity mobile)
    {
        return _sessions is not null &&
               _sessions.TryGetByCharacterId(mobile.Id, out var session) &&
               session.AccountType >= AccountType.GameMaster;
    }

    // A height is 127 at most.
    private static Point3D EyeOf(MobileEntity mobile)
    {
        var spot = mobile.Location;

        return new(spot.X, spot.Y, Math.Min(spot.Z + EyeHeight, sbyte.MaxValue));
    }

    // The props a spawn region gives its NPCs. A script may have written anything there: what is not four numbers
    // in order is no home, rather than an error on every think.
    private static Rectangle2D? HomeOf(MobileEntity npc)
    {
        try
        {
            return npc.TryGetProp<long>(HomeX1, out var x1) &&
                   npc.TryGetProp<long>(HomeY1, out var y1) &&
                   npc.TryGetProp<long>(HomeX2, out var x2) &&
                   npc.TryGetProp<long>(HomeY2, out var y2) &&
                   x1 <= x2 &&
                   y1 <= y2
                ? new Rectangle2D(new Point2D((int)x1, (int)y1), new Point2D((int)x2, (int)y2))
                : null;
        }
        catch (Exception exception) when (exception is InvalidCastException or FormatException or OverflowException)
        {
            return null;
        }
    }

    // The corners of a home are both inside it.
    private static bool Contains(Rectangle2D home, int x, int y)
    {
        return x >= home.Start.X && x <= home.End.X && y >= home.Start.Y && y <= home.End.Y;
    }

    // The cell a step in a direction leads to.
    private static (int X, int Y) Offset(DirectionType direction)
    {
        return (direction & DirectionMask) switch
        {
            DirectionType.North     => (0, -1),
            DirectionType.NorthEast => (1, -1),
            DirectionType.East      => (1, 0),
            DirectionType.SouthEast => (1, 1),
            DirectionType.South     => (0, 1),
            DirectionType.SouthWest => (-1, 1),
            DirectionType.West      => (-1, 0),
            _                       => (-1, -1)
        };
    }

    // As its template says: a water mobile swims, an amphibious one walks and swims.
    private MovementAbilityType AbilityOf(MobileEntity npc)
    {
        var movement = npc.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.Movement : null;

        return movement switch
        {
            MobileMovementType.Water => MovementAbilityType.Swim,
            MobileMovementType.Both => MovementAbilityType.Walk | MovementAbilityType.Swim,
            _ => MovementAbilityType.Walk
        };
    }

    private bool TryGetNpc(long serial, [NotNullWhen(true)] out MobileEntity? npc)
    {
        npc = null;

        return serial is > 0 and <= uint.MaxValue &&
               _mobiles.TryGet(new Serial((uint)serial), out npc) &&
               npc.IsNpc &&
               _mobiles.IsInWorld(npc.Id);
    }
}
