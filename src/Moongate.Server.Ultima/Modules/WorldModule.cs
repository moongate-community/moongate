using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules.Internal;
using Moongate.Server.Ultima.Types.World;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>world</c> Lua module: what a script can ask about the world around its NPC or item, such as whether a door's
///     doorway is free.
/// </summary>
[ScriptModule("world", "Asks about the world: who stands where, whether a place is guarded, what time it is, the moons.")]
public sealed class WorldModule
{
    public const int MaximumRange = 32;

    private readonly ISectorService _sectors;
    private readonly IClockService _clock;
    private readonly TimeProvider _time;
    private readonly IWorldPropsService? _props;
    private readonly ISessionService _sessions;
    private readonly IItemService _items;
    private readonly IRegionService _regions;
    private readonly ILineOfSightService? _sight;
    private readonly IMovementService? _movement;
    private readonly IWeatherService? _weather;
    private readonly ISeasonService? _seasons;
    private readonly IBroadcastService? _broadcast;
    private readonly IMobileService? _mobiles;
    private readonly ILogger _logger = Log.ForContext<WorldModule>();

    public WorldModule(
        ISectorService sectors,
        IClockService clock,
        ISessionService sessions,
        IItemService items,
        IRegionService regions,
        ILineOfSightService? sight = null,
        IMovementService? movement = null,
        IWeatherService? weather = null,
        ISeasonService? seasons = null,
        IBroadcastService? broadcast = null,
        IMobileService? mobiles = null,
        TimeProvider? time = null,
        IWorldPropsService? props = null
    )
    {
        _props = props;
        _time = time ?? TimeProvider.System;
        _sight = sight;
        _movement = movement;
        _weather = weather;
        _seasons = seasons;
        _broadcast = broadcast;
        _mobiles = mobiles;
        _regions = regions;
        _sectors = sectors;
        _clock = clock;
        _sessions = sessions;
        _items = items;
    }

    /// <summary>
    ///     Gets whether <paramref name="mobile" /> wears or carries, at any depth of its containers, an item whose prop
    ///     <paramref name="key" /> is <paramref name="value" />, such as the key of a door;
    ///     <c>world.carries(user, "key.value", 1234)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the mobile wears or carries, in its containers at any depth, an item whose prop key is value.")]
    public bool Carries(long mobile, string key, object value)
    {
        if (mobile is <= 0 or > uint.MaxValue || !ScriptPropValue.TryFromLua(value, out var wanted))
        {
            return false;
        }

        // What lies in the bank is not carried.
        return _items.GetOwnedBy(new Serial((uint)mobile))
                     .Any(
                         item => item.Props?.GetValueOrDefault(key) is { } prop &&
                                 Equals(prop, wanted) &&
                                 _items.GetWornRoot(item)?.Layer != LayerType.Bank
                     );
    }

    /// <summary>
    ///     Gets a value the shard as a whole keeps across restarts; <c>world.get_prop("event.day")</c>.
    /// </summary>
    [ScriptFunction(helpText: "A value the whole shard keeps across restarts: a string, a number or a bool; nil when there is none.")]
    public object? GetProp(string key)
    {
        return _props?.Get(key);
    }

    /// <summary>
    ///     Keeps a value for the whole shard, saved with the world, or removes it for <c>nil</c>;
    ///     <c>world.set_prop("event.day", 12)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Keeps a string, a number or a bool for the whole shard across restarts, nil removes it; false for a table, a function or a blank key.")]
    public bool SetProp(string key, object? value = null)
    {
        if (_props is null || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (value is null)
        {
            _props.Set(key, null);

            return true;
        }

        if (!ScriptPropValue.TryFromLua(value, out var prop))
        {
            return false;
        }

        _props.Set(key, prop);

        return true;
    }

    /// <summary>
    ///     Gets the real time as the seconds since 1970 (UTC), to keep in a prop when something happens next, such as a
    ///     container's next refill; <c>world.now()</c>.
    /// </summary>
    [ScriptFunction(helpText: "The real time as whole seconds since 1970 (UTC): keep world.now() + 3600 in a prop to do something an hour from now, also after a restart.")]
    public long Now()
    {
        return _time.GetUtcNow().ToUnixTimeSeconds();
    }

    /// <summary>
    ///     Gets whether <paramref name="player" /> is a game master or an administrator in the world, such as to let staff
    ///     use a protected light; <c>world.is_staff(user)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the player is a game master or an administrator; false for an NPC or a player not in the world.")]
    public bool IsStaff(long player)
    {
        return player is > 0 and <= uint.MaxValue &&
               _sessions.TryGetByCharacterId(new Serial((uint)player), out var session) &&
               session.AccountType >= AccountType.GameMaster;
    }

    /// <summary>
    ///     Gets whether a player or an NPC stands on the tile <paramref name="x" />, <paramref name="y" /> of
    ///     <paramref name="map" />; <c>world.is_occupied(MapType.Trammel, x, y)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether a player or an NPC stands on the tile x, y of the map, at any height.")]
    public bool IsOccupied(MapType map, int x, int y)
    {
        return _sectors.GetMobilesInRange(map, new Point3D(x, y, 0), 0).Count > 0;
    }

    /// <summary>
    ///     Gets whether guards protect the place <paramref name="x" />, <paramref name="y" />, <paramref name="z" /> of
    ///     <paramref name="map" />, such as a town; <c>world.is_guarded(MapType.Trammel, 1496, 1628, 10)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the region of the place x, y, z of the map is guarded; false outside every region or for a z outside -128 to 127.")]
    public bool IsGuarded(MapType map, int x, int y, int z)
    {
        return z is >= sbyte.MinValue and <= sbyte.MaxValue && _regions.Find(map, new Point3D(x, y, z))?.Guarded == true;
    }

    /// <summary>
    ///     Gets the name of the region of the place <paramref name="x" />, <paramref name="y" />, <paramref name="z" />
    ///     of <paramref name="map" />; <c>world.region(MapType.Trammel, 1496, 1628, 10) == "Britain"</c>.
    /// </summary>
    [ScriptFunction(helpText: "The name of the region of the place x, y, z of the map; nil outside every region or for a z outside -128 to 127.")]
    public string? Region(MapType map, int x, int y, int z)
    {
        return z is >= sbyte.MinValue and <= sbyte.MaxValue ? _regions.Find(map, new Point3D(x, y, z))?.Name : null;
    }

    /// <summary>
    ///     Gets the mobiles, players and NPCs, within <paramref name="range" /> tiles of a place as a list of serials;
    ///     <c>for _, who in ipairs(world.mobiles_in_range(here.map, here.x, here.y, 5)) do ... end</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serials of the players and NPCs within range tiles (0 to 32) of x, y on the map, at any height, as a list.")]
    public LuaTable MobilesInRange(MapType map, int x, int y, int range)
    {
        return Serials(
            range is < 0 or > MaximumRange
                ? []
                : _sectors.GetMobilesInRange(map, new Point3D(x, y, 0), range).Select(mobile => mobile.Id)
        );
    }

    /// <summary>
    ///     Gets the items lying on the ground within <paramref name="range" /> tiles of a place as a list of serials;
    ///     <c>world.items_in_range(here.map, here.x, here.y, 2)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serials of the items on the ground within range tiles (0 to 32) of x, y on the map, at any height, as a list.")]
    public LuaTable ItemsInRange(MapType map, int x, int y, int range)
    {
        return Serials(
            range is < 0 or > MaximumRange ? [] : _sectors.GetItemsInRange(map, new Point3D(x, y, 0), range).Select(item => item.Id)
        );
    }

    /// <summary>
    ///     Gets the players in the world as a list of serials; <c>#world.players()</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serials of the players' characters in the world, as a list.")]
    public LuaTable Players()
    {
        return Serials(_sessions.GetAll().Where(session => session.CharacterId.IsValid).Select(session => session.CharacterId));
    }

    /// <summary>
    ///     Gets whether nothing stands between two places of a map, as for a spell or an arrow;
    ///     <c>world.line_of_sight(MapType.Trammel, 1496, 1628, 10, 1500, 1630, 10)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the place x2, y2, z2 is in sight of x1, y1, z1 on the map; false beyond the range a line of sight is checked at, on a map that is not loaded or for a z outside -128 to 127.")]
    public bool LineOfSight(MapType map, int x1, int y1, int z1, int x2, int y2, int z2)
    {
        if (_sight is null || z1 is < sbyte.MinValue or > sbyte.MaxValue || z2 is < sbyte.MinValue or > sbyte.MaxValue)
        {
            return false;
        }

        try
        {
            return _sight.HasLineOfSight(map, new Point3D(x1, y1, z1), new Point3D(x2, y2, z2));
        }
        catch (KeyNotFoundException)
        {
            // The map is not loaded.
            return false;
        }
    }

    /// <summary>
    ///     Gets the height a mobile can stand at on a cell, looking down from <paramref name="z" />, such as before
    ///     teleporting someone there; <c>world.standing_z(MapType.Trammel, 1496, 1628, 20)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The height a mobile can stand at on the cell x, y of the map, at or below z; nil when nothing there can be stood on.")]
    public int? StandingZ(MapType map, int x, int y, int z)
    {
        return _movement is not null && _sectors.IsInside(map, x, y) && _movement.TryGetSpawnZ(map, x, y, z, out var found)
            ? found
            : null;
    }

    /// <summary>
    ///     Gets the weather a player stands in as <c>{ kind, density, temperature }</c>;
    ///     <c>world.weather(who).kind == WeatherKindType.Rain</c>.
    /// </summary>
    [ScriptFunction(helpText: "The weather where the player stands, as a table { kind (a WeatherKindType), density, temperature }; nil for an NPC or a player not in the world.")]
    public LuaTable? Weather(long player)
    {
        if (_weather is null ||
            _mobiles is null ||
            player is <= 0 or > uint.MaxValue ||
            !_mobiles.TryGet(new Serial((uint)player), out var mobile) ||
            mobile.IsNpc)
        {
            return null;
        }

        var state = _weather.StateOf(_weather.ProfileOf(mobile));
        var table = new LuaTable();
        table["kind"] = (int)state.Kind;
        table["density"] = state.Density;
        table["temperature"] = state.Temperature;

        return table;
    }

    /// <summary>
    ///     Gets the season of a map; <c>world.season(MapType.Trammel) == SeasonType.Winter</c>.
    /// </summary>
    [ScriptFunction(helpText: "The season of the map, a SeasonType; nil when the seasons are not running.")]
    public SeasonType? Season(MapType map)
    {
        return _seasons?.SeasonOf(map);
    }

    /// <summary>
    ///     Sends <paramref name="text" /> to every player in the world as a system message;
    ///     <c>world.broadcast("The gates of Britain are open.")</c>.
    /// </summary>
    [ScriptFunction(helpText: "Sends a system message (cut to 128 characters) to every player in the world; false for a blank text.")]
    public bool Broadcast(string text)
    {
        if (_broadcast is null || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var message = text.Length > ItemModule.MaximumTextLength ? text[..ItemModule.MaximumTextLength] : text;

        // Sent off the script: a failure is logged, never raised into it.
        _ = _broadcast.BroadcastAsync(message)
                      .ContinueWith(
                          task => _logger.Warning(task.Exception, "world.broadcast failed"),
                          CancellationToken.None,
                          TaskContinuationOptions.OnlyOnFaulted,
                          TaskScheduler.Default
                      );

        return true;
    }

    /// <summary>
    ///     Gets the phase of a moon, <paramref name="moon" /> being <c>MapType.Trammel</c> or <c>MapType.Felucca</c>, seen
    ///     from the column <paramref name="x" />, as the spyglass shows it;
    ///     <c>world.moon(MapType.Trammel, x) == MoonPhaseType.FullMoon</c>.
    /// </summary>
    [ScriptFunction(helpText: "The phase of the moon (MapType.Trammel or MapType.Felucca) seen from the column x, a MoonPhaseType.")]
    public MoonPhaseType Moon(MapType moon, int x)
    {
        return _clock.GetMoonPhase(moon, x);
    }

    /// <summary>
    ///     Gets the time of day on <paramref name="map" /> at the column <paramref name="x" />, as a table
    ///     <c>{ hours, minutes }</c>; <c>world.time(MapType.Trammel, x).hours</c>.
    /// </summary>
    [ScriptFunction(helpText: "The time of day on the map at the column x, as a table { hours, minutes }.")]
    public LuaTable Time(MapType map, int x)
    {
        var time = _clock.GetTime(map, x);
        var table = new LuaTable();
        table["hours"] = time.Hours;
        table["minutes"] = time.Minutes;

        return table;
    }

    private static LuaTable Serials(IEnumerable<Serial> serials)
    {
        var table = new LuaTable();
        var index = 1;

        foreach (var serial in serials)
        {
            table[index++] = (long)serial.Value;
        }

        return table;
    }
}
