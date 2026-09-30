using Lua;
using Moongate.Core.Geometry;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>world</c> Lua module: what a script can ask about the world around its NPC or item, such as whether a door's
///     doorway is free.
/// </summary>
[ScriptModule("world", "Asks about the world: who stands where, what time it is.")]
public sealed class WorldModule
{
    private readonly ISectorService _sectors;
    private readonly IClockService _clock;

    public WorldModule(ISectorService sectors, IClockService clock)
    {
        _sectors = sectors;
        _clock = clock;
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
}
