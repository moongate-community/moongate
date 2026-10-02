using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Modules.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>mobile</c> Lua module: what a script does to a mobile in the world, a player or an NPC, by serial, such as
///     the teleporter that moves whoever walks onto it; <c>mobile.teleport(who, x, y, z)</c>. A serial that is not a
///     mobile in the world gives <c>false</c> or <c>nil</c>, never an error.
/// </summary>
[ScriptModule("mobile", "Acts on a mobile in the world, a player or an NPC: teleports it, reads where it is, plays a sound on it, tells a player something.")]
public sealed class MobileModule
{
    private readonly IMobileService _mobiles;
    private readonly ITeleportService _teleports;
    private readonly ISpeechService _speech;
    private readonly IItemService? _items;
    private readonly IMusicService? _music;
    private readonly IRegionService? _regions;
    private readonly ILightService? _light;

    public MobileModule(
        IMobileService mobiles,
        ITeleportService teleports,
        ISpeechService speech,
        IItemService? items = null,
        IMusicService? music = null,
        IRegionService? regions = null,
        ILightService? light = null
    )
    {
        _mobiles = mobiles;
        _teleports = teleports;
        _speech = speech;
        _items = items;
        _music = music;
        _regions = regions;
        _light = light;
    }

    /// <summary>
    ///     Gets the mobile's name; <c>mobile.name(who)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The mobile's name; nil for a mobile not in the world.")]
    public string? Name(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? mobile.Name : null;
    }

    /// <summary>
    ///     Gets whether the serial is a player's character in the world; <c>mobile.is_player(who)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Whether the serial is a player's character in the world; false for an NPC or an unknown serial.")]
    public bool IsPlayer(long serial)
    {
        return TryGetMobile(serial, out var mobile) && !mobile.IsNpc;
    }

    /// <summary>
    ///     Gets the direction the mobile faces; <c>mobile.direction(who) == DirectionType.North</c>.
    /// </summary>
    [ScriptFunction(helpText: "The direction the mobile faces, a DirectionType; nil for a mobile not in the world.")]
    public DirectionType? Direction(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? mobile.Direction : null;
    }

    /// <summary>
    ///     Gets the mobile's body, strength, hit points and the like as a table; <c>mobile.stats(who).hits</c>.
    /// </summary>
    [ScriptFunction(helpText: "The mobile's numbers as a table: body, strength, dexterity, intelligence, hits, hits_max, mana, mana_max, stamina, stamina_max, fame, karma; nil for a mobile not in the world.")]
    public LuaTable? Stats(long serial)
    {
        if (!TryGetMobile(serial, out var mobile))
        {
            return null;
        }

        var table = new LuaTable();
        table["body"] = mobile.Body;
        table["strength"] = mobile.Strength;
        table["dexterity"] = mobile.Dexterity;
        table["intelligence"] = mobile.Intelligence;
        table["hits"] = mobile.Hits;
        table["hits_max"] = mobile.HitsMax;
        table["mana"] = mobile.Mana;
        table["mana_max"] = mobile.ManaMax;
        table["stamina"] = mobile.Stamina;
        table["stamina_max"] = mobile.StaminaMax;
        table["fame"] = mobile.Fame;
        table["karma"] = mobile.Karma;

        return table;
    }

    /// <summary>
    ///     Gets the backpack the mobile wears, to look into it with <c>item.contents</c>; <c>mobile.backpack(who)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The serial of the backpack the mobile wears; nil for a mobile without one or not in the world.")]
    public long? Backpack(long serial)
    {
        return TryGetMobile(serial, out var mobile) &&
               _items?.GetWorn(mobile.Id).FirstOrDefault(item => item.Layer == LayerType.Backpack) is { } backpack
            ? backpack.Id.Value
            : null;
    }

    /// <summary>
    ///     Gets the name of the region the mobile stands in; <c>mobile.region(who) == "Britain"</c>.
    /// </summary>
    [ScriptFunction(helpText: "The name of the region the mobile stands in; nil outside every region or for a mobile not in the world.")]
    public string? Region(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? _regions?.Find(mobile.Map, mobile.Location)?.Name : null;
    }

    /// <summary>
    ///     Gets the light level where the mobile stands, 0 the brightest day and 30 the darkest; <c>mobile.light(who)</c>.
    /// </summary>
    [ScriptFunction(helpText: "The light level where the mobile stands, 0 (day) to 30 (dark): the hour, or its dungeon's; nil for a mobile not in the world.")]
    public int? Light(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? _light?.LevelFor(mobile) : null;
    }

    /// <summary>
    ///     Plays a music to a player, until its region gives it another; <c>mobile.play_music(who, MusicType.Britain1)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays a music (a MusicType) to the player; false for an NPC or a player not in the world.")]
    public bool PlayMusic(long serial, MusicType music)
    {
        return _music is not null && TryGetMobile(serial, out var mobile) && !mobile.IsNpc && _music.Play(mobile, music);
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> the mobile keeps, saved with it across restarts;
    ///     <c>mobile.get_prop(who, "quest.step")</c>.
    /// </summary>
    [ScriptFunction(helpText: "A value the mobile, a player or an NPC, keeps across restarts: a string, a number or a bool; nil when it has none.")]
    public object? GetProp(long serial, string key)
    {
        return TryGetMobile(serial, out var mobile) && mobile.Props is { } props && props.TryGetValue(key, out var value)
            ? value
            : null;
    }

    /// <summary>
    ///     Keeps <paramref name="value" /> as the prop <paramref name="key" /> of the mobile, saved with it, or removes
    ///     it for <c>nil</c>; <c>mobile.set_prop(who, "quest.step", 2)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Keeps a string, a number or a bool on the mobile across restarts, nil removes it; false for a table, a function, a blank key or a mobile not in the world.")]
    public bool SetProp(long serial, string key, object? value = null)
    {
        if (string.IsNullOrWhiteSpace(key) || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        if (value is null)
        {
            mobile.RemoveProp(key);

            return true;
        }

        if (!ScriptPropValue.TryFromLua(value, out var prop))
        {
            return false;
        }

        mobile.SetProp(key, prop);

        return true;
    }

    /// <summary>
    ///     Teleports the mobile to <paramref name="x" />, <paramref name="y" />, <paramref name="z" /> of its own map, or
    ///     of <paramref name="map" /> when given; <c>mobile.teleport(who, 5690, 569, 25)</c>,
    ///     <c>mobile.teleport(who, 259, 785, 64, MapType.Tokuno)</c>. A player's client is told where it stands, after the
    ///     map change when there is one; the players around the old spot lose the mobile and those around the new one
    ///     see it.
    /// </summary>
    [ScriptFunction(helpText: "Teleports the mobile to x, y, z on its map, or on the given map (a MapType or its name); false for a mobile not in the world, a map that does not exist or is not loaded, a spot outside the map or a z outside -128 to 127.")]
    public bool Teleport(long serial, int x, int y, int z, object? map = null)
    {
        if (z is < sbyte.MinValue or > sbyte.MaxValue || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        // A MapType as scripts see it, a number, or its name, as a prop set by hand may hold it.
        MapType? destination = map switch
        {
            null => mobile.Map,
            double number when Math.Floor(number) == number &&
                               number is >= byte.MinValue and <= byte.MaxValue &&
                               Enum.IsDefined((MapType)(byte)number) => (MapType)(byte)number,
            string name when !name.Any(char.IsDigit) && Enum.TryParse<MapType>(name, true, out var named) => named,
            _ => null
        };

        return destination is { } target && _teleports.Teleport(mobile, target, new Point3D(x, y, z));
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

    /// <summary>
    ///     Sends <paramref name="text" /> to the player as a system message, in the lower left of its screen;
    ///     <c>mobile.message(who, "That is too far away.")</c>.
    /// </summary>
    [ScriptFunction(helpText: "A system message read only by that player; false for an empty text, an NPC or a player not in the world.")]
    public bool Message(long serial, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        return _speech.Tell(mobile, text.Length > ItemModule.MaximumTextLength ? text[..ItemModule.MaximumTextLength] : text);
    }

    private bool TryGetMobile(long serial, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        mobile = null;

        return serial is > 0 and <= uint.MaxValue && _mobiles.TryGet(new Serial((uint)serial), out mobile);
    }
}
