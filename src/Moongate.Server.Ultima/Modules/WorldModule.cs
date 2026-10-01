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

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>world</c> Lua module: what a script can ask about the world around its NPC or item, such as whether a door's
///     doorway is free.
/// </summary>
[ScriptModule("world", "Asks about the world: who stands where, what time it is, the moons.")]
public sealed class WorldModule
{
    private readonly ISectorService _sectors;
    private readonly IClockService _clock;
    private readonly ISessionService _sessions;
    private readonly IItemService _items;

    public WorldModule(ISectorService sectors, IClockService clock, ISessionService sessions, IItemService items)
    {
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

        return _items.GetOwnedBy(new Serial((uint)mobile))
                     .Any(item => item.Props?.GetValueOrDefault(key) is { } prop && Equals(prop, wanted));
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
}
