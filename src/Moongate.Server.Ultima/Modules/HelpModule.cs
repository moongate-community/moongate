using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Cities;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>help</c> Lua module: the settings of the help gump and the starting city nearest to a character, for the
///     "I am stuck" button; <c>help.nearest_city(player)</c>.
/// </summary>
[ScriptModule(
    "help",
    "What the help gump needs from the server: the wait and the pause of the I am stuck button, and the starting city nearest to a character."
)]
public sealed class HelpModule
{
    private readonly IMobileService _mobiles;
    private readonly IDataLoaderService _data;
    private readonly HelpConfig _config;

    public HelpModule(IMobileService mobiles, IDataLoaderService data, HelpConfig config)
    {
        _mobiles = mobiles;
        _data = data;
        _config = config;
    }

    /// <summary>
    ///     Gets the starting city nearest to a character on its map; <c>local city = help.nearest_city(player)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The starting city of data/starting_cities.toml nearest to the character, on the map it stands on, as { town, x, y, z, map } (map is the MapType number, for mobile.teleport). When no city is on that map it is the first city of the file. Nil when the mobile is not in the world or the file has no city."
    )]
    public LuaTable? NearestCity(long serial)
    {
        if (serial is <= 0 or > uint.MaxValue || !_mobiles.TryGet(new Serial((uint)serial), out var mobile))
        {
            return null;
        }

        var cities = _data.GetEntities<StartingCityContent>();
        var city = cities
                       .Where(candidate => candidate.Map == mobile.Map)
                       .OrderBy(candidate => Squared(candidate, mobile.Location.X, mobile.Location.Y))
                       .FirstOrDefault() ??
                   (cities.Count > 0 ? cities[0] : null);

        if (city is null)
        {
            return null;
        }

        var table = new LuaTable();
        table["town"] = city.Town;
        table["x"] = city.Location.X;
        table["y"] = city.Location.Y;
        table["z"] = city.Location.Z;
        table["map"] = (int)city.Map;

        return table;
    }

    /// <summary>
    ///     Gets the settings of the gump; <c>local s = help.settings()</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The settings of ultima.help as { wait_seconds, cooldown_minutes }: the seconds a character must stand still before I am stuck moves it, and the minutes before it can ask again (0 for no pause)."
    )]
    public LuaTable Settings()
    {
        var table = new LuaTable();
        table["wait_seconds"] = _config.StuckWaitSeconds;
        table["cooldown_minutes"] = _config.StuckCooldownMinutes;

        return table;
    }

    private static long Squared(StartingCityContent city, int x, int y)
    {
        long dx = city.Location.X - x;
        long dy = city.Location.Y - y;

        return dx * dx + dy * dy;
    }
}
