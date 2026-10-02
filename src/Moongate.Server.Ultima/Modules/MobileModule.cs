using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>mobile</c> Lua module: what a script does to a mobile in the world, a player or an NPC, by serial, such as
///     the teleporter that moves whoever walks onto it; <c>mobile.teleport(who, x, y, z)</c>. A serial that is not a
///     mobile in the world gives <c>false</c> or <c>nil</c>, never an error.
/// </summary>
[ScriptModule("mobile", "Acts on a mobile in the world, a player or an NPC: teleports it, reads where it is, plays a sound on it.")]
public sealed class MobileModule
{
    private readonly IMobileService _mobiles;
    private readonly ITeleportService _teleports;
    private readonly ISpeechService _speech;

    public MobileModule(IMobileService mobiles, ITeleportService teleports, ISpeechService speech)
    {
        _mobiles = mobiles;
        _teleports = teleports;
        _speech = speech;
    }

    /// <summary>
    ///     Teleports the mobile to <paramref name="x" />, <paramref name="y" />, <paramref name="z" /> of its own map;
    ///     <c>mobile.teleport(who, 5690, 569, 25)</c>. A player's client is told where it stands; the players around the old
    ///     spot lose the mobile and those around the new one see it.
    /// </summary>
    [ScriptFunction(helpText: "Teleports the mobile to x, y, z on its map; false for a mobile not in the world, a spot outside the map or a z outside -128 to 127.")]
    public bool Teleport(long serial, int x, int y, int z)
    {
        return z is >= sbyte.MinValue and <= sbyte.MaxValue &&
               TryGetMobile(serial, out var mobile) &&
               _teleports.Teleport(mobile, new Point3D(x, y, z));
    }

    /// <summary>
    ///     Gets where the mobile stands as <c>{ x, y, z, map }</c>; <c>mobile.location(who)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Where the mobile stands, as a table { x, y, z, map }; nil for a mobile not in the world.")]
    public LuaTable? Location(long serial)
    {
        if (!TryGetMobile(serial, out var mobile))
        {
            return null;
        }

        var table = new LuaTable();
        table["x"] = mobile.Location.X;
        table["y"] = mobile.Location.Y;
        table["z"] = mobile.Location.Z;
        table["map"] = (int)mobile.Map;

        return table;
    }

    /// <summary>
    ///     Plays <paramref name="sound" /> where the mobile stands for the players within 15 cells;
    ///     <c>mobile.play_sound(who, 0x1FE)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays a sound id (0 to 65535) where the mobile stands; false for a mobile not in the world or an unknown sound.")]
    public bool PlaySound(long serial, int sound)
    {
        if (sound is < 0 or > ushort.MaxValue || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        _speech.PlaySound(mobile, sound);

        return true;
    }

    private bool TryGetMobile(long serial, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        mobile = null;

        return serial is > 0 and <= uint.MaxValue && _mobiles.TryGet(new Serial((uint)serial), out mobile);
    }
}
