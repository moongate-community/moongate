using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
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
        IMovementService? movement = null
    )
    {
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
    [ScriptFunction(helpText: "The NPC says text overhead to the players nearby; false for an unknown NPC or blank text.")]
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
    [ScriptFunction(helpText: "Plays a sound id (0 to 65535) or a kind of the NPC's template sounds (start_attack, idle, attack, hurt, death) where the NPC stands; false for an unknown NPC, sound or kind.")]
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
    [ScriptFunction(helpText: "One step in a direction, a run when running is true, turning first when needed; false when blocked.")]
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
    [ScriptFunction(helpText: "One step along a path to x, y (z defaults to the ground there), a run when running is true: 'arrived' within range tiles of it, 'moving' after a step, 'blocked' when the step was refused or it waits to look for another way, 'no_path' when the last search did not reach the place; nil for an unknown NPC, a negative range or a z outside -128 to 127.")]
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
    [ScriptFunction(helpText: "The steps from the NPC to x, y (z defaults to the ground there) as a list of DirectionType; with partial true, the steps to the closest place when it cannot be reached. Nil when there is no path, the place is too far or the NPC is unknown.")]
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
    [ScriptFunction(helpText: "Spawns an NPC of a mobile template at x, y, z of the map, a moment later; the optional function gets its serial. False for an unknown template, a spot outside the map or a z outside -128 to 127.")]
    public bool Spawn(string template, MapType map, int x, int y, int z, LuaValue callback = default)
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

        _ = _npcs.RemoveAsync(npc.Id)
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
    [ScriptFunction(helpText: "Turns the NPC towards x, y, seen by the players in range; false for an unknown NPC or its own cell.")]
    public bool Face(long serial, int x, int y)
    {
        if (!TryGetNpc(serial, out var npc) || npc.Location.X == x && npc.Location.Y == y)
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
    ///     Gets how many tiles lie between the NPC and a place, as the view range counts them;
    ///     <c>npc.distance_to(serial, there.x, there.y) <= 2</c>.
    /// </summary>
    [ScriptFunction(helpText: "The tiles between the NPC and x, y, the larger of the two differences; nil for an unknown NPC.")]
    public int? DistanceTo(long serial, int x, int y)
    {
        return TryGetNpc(serial, out var npc)
            ? Math.Max(Math.Abs(npc.Location.X - x), Math.Abs(npc.Location.Y - y))
            : null;
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
    [ScriptFunction(helpText: "Keeps a string, a number or a bool on the NPC across restarts, nil removes it; false for a table, a function or a blank key.")]
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
