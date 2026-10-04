using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Effects;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>effect</c> Lua module: graphic effects for the players around, such as the smoke of a teleport, a fireball
///     or a lightning bolt; <c>effect.at(map, x, y, z, EffectGraphicType.Smoke)</c>. An effect takes an optional table of
///     options: <c>speed</c>, <c>duration</c>, <c>hue</c>, <c>render</c> (an <c>EffectRenderModeType</c>),
///     <c>fixed_direction</c>, <c>explodes</c>, and for the Enhanced Client <c>particle</c>, <c>explode_particle</c>,
///     <c>explode_sound</c> and <c>layer</c> (an <c>EffectLayerType</c>). A value out of range, an option of the wrong
///     type or an unknown option gives <c>false</c> and plays nothing; an argument of the wrong type raises an error,
///     as for every module.
/// </summary>
[ScriptModule("effect", "Plays graphic effects for the players around: at a point, on a mobile or an item, flying between two, or a lightning bolt.")]
public sealed class EffectModule
{
    private static readonly HashSet<string> OptionNames =
    [
        "speed", "duration", "hue", "render", "fixed_direction", "explodes", "particle", "explode_particle",
        "explode_sound", "layer"
    ];

    private readonly IEffectService _effects;
    private readonly IMobileService _mobiles;
    private readonly IItemService _items;

    public EffectModule(IEffectService effects, IMobileService mobiles, IItemService items)
    {
        _effects = effects;
        _mobiles = mobiles;
        _items = items;
    }

    /// <summary>
    ///     Plays an animation that stays at a point of a map; <c>effect.at(map, x, y, z, EffectGraphicType.Smoke)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays an effect graphic that stays at x, y, z of a map (a MapType number); false for a value out of range.")]
    public bool At(int map, int x, int y, int z, int graphic, LuaTable? options = null)
    {
        if (!Enum.IsDefined((MapType)map) ||
            x is < 0 or > ushort.MaxValue ||
            y is < 0 or > ushort.MaxValue ||
            z is < sbyte.MinValue or > sbyte.MaxValue ||
            !TryReadOptions(graphic, options, out var effect))
        {
            return false;
        }

        _effects.PlayAt((MapType)map, new Point3D(x, y, z), effect);

        return true;
    }

    /// <summary>
    ///     Plays an animation on a mobile or on an item lying on the ground;
    ///     <c>effect.on(who, EffectGraphicType.SparkleHeal)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays an effect graphic on a mobile, which it follows, or on a ground item; false for something not in the world or a value out of range.")]
    public bool On(long serial, int graphic, LuaTable? options = null)
    {
        if (!TryLocate(serial, out var target, out var map, out var location) ||
            !TryReadOptions(graphic, options, out var effect))
        {
            return false;
        }

        _effects.PlayOn(target, map, location, effect);

        return true;
    }

    /// <summary>
    ///     Plays an animation flying from one mobile or ground item to another on the same map;
    ///     <c>effect.moving(caster, target, EffectGraphicType.LargeFireball, { explodes = true })</c>.
    /// </summary>
    [ScriptFunction(helpText: "Plays an effect graphic flying from one mobile or ground item to another on the same map; false when one is not in the world, they are on two maps or a value is out of range.")]
    public bool Moving(long from, long to, int graphic, LuaTable? options = null)
    {
        if (!TryLocate(from, out var source, out var map, out var start) ||
            !TryLocate(to, out var target, out var targetMap, out var end) ||
            map != targetMap ||
            !TryReadOptions(graphic, options, out var effect))
        {
            return false;
        }

        _effects.PlayMoving(map, source, start, target, end, effect);

        return true;
    }

    /// <summary>
    ///     Strikes a mobile or a ground item with a lightning bolt; <c>effect.lightning(who)</c>.
    /// </summary>
    [ScriptFunction(helpText: "Strikes a mobile or a ground item with a lightning bolt, optionally of a hue; false for something not in the world or a hue out of range.")]
    public bool Lightning(long serial, int hue = 0)
    {
        if (hue is < 0 or > ushort.MaxValue || !TryLocate(serial, out var target, out var map, out var location))
        {
            return false;
        }

        _effects.PlayLightning(target, map, location, new Hue((ushort)hue));

        return true;
    }

    // A mobile in the world, or an item lying on the ground: what has a place of its own on a map.
    private bool TryLocate(long serial, out Serial target, out MapType map, out Point3D location)
    {
        target = default;
        map = default;
        location = default;

        if (serial is <= 0 or > uint.MaxValue)
        {
            return false;
        }

        target = new Serial((uint)serial);

        if (_mobiles.TryGet(target, out var mobile))
        {
            map = mobile.Map;
            location = mobile.Location;

            return true;
        }

        if (_items.TryGet(target, out var item) && item.Map is { } itemMap && item.GroundLocation is { } spot)
        {
            map = itemMap;
            location = spot;

            return true;
        }

        return false;
    }

    private static bool TryReadOptions(int graphic, LuaTable? table, [NotNullWhen(true)] out EffectOptions? options)
    {
        options = null;
        var defaults = new EffectOptions();

        if (graphic is < 0 or > ushort.MaxValue ||
            !HasOnlyKnownKeys(table) ||
            !TryReadNumber(table, "speed", defaults.Speed, byte.MaxValue, out var speed) ||
            !TryReadNumber(table, "duration", defaults.Duration, byte.MaxValue, out var duration) ||
            !TryReadNumber(table, "hue", 0, ushort.MaxValue, out var hue) ||
            !TryReadNumber(table, "render", 0, int.MaxValue, out var render) ||
            !TryReadNumber(table, "particle", 0, ushort.MaxValue, out var particle) ||
            !TryReadNumber(table, "explode_particle", 0, ushort.MaxValue, out var explodeParticle) ||
            !TryReadNumber(table, "explode_sound", 0, ushort.MaxValue, out var explodeSound) ||
            !TryReadNumber(table, "layer", (int)EffectLayerType.None, byte.MaxValue, out var layer) ||
            !TryReadFlag(table, "fixed_direction", out var fixedDirection) ||
            !TryReadFlag(table, "explodes", out var explodes) ||
            !Enum.IsDefined((EffectRenderModeType)render) ||
            !Enum.IsDefined((EffectLayerType)layer) ||
            graphic == 0 && particle == 0)
        {
            return false;
        }

        options = new()
        {
            Graphic = graphic,
            Speed = (byte)speed,
            Duration = (byte)duration,
            Hue = new Hue((ushort)hue),
            RenderMode = (EffectRenderModeType)render,
            FixedDirection = fixedDirection,
            Explodes = explodes,
            Particle = particle,
            ExplodeParticle = explodeParticle,
            ExplodeSound = explodeSound,
            Layer = (EffectLayerType)layer
        };

        return true;
    }

    // A misspelled option would otherwise be ignored in silence: the effect is refused instead.
    private static bool HasOnlyKnownKeys(LuaTable? table)
    {
        if (table is null)
        {
            return true;
        }

        foreach (var (key, _) in table)
        {
            if (!key.TryRead<string>(out var name) || !OptionNames.Contains(name))
            {
                return false;
            }
        }

        return true;
    }

    // A missing key gives the fallback; a value that is not a whole number from 0 to the maximum refuses the effect.
    private static bool TryReadNumber(LuaTable? table, string key, int fallback, int maximum, out int value)
    {
        value = fallback;

        if (table is null || table[key].Type == LuaValueType.Nil)
        {
            return true;
        }

        if (table[key].Type != LuaValueType.Number ||
            !table[key].TryRead<double>(out var number) ||
            number < 0 ||
            number > maximum ||
            number != Math.Floor(number))
        {
            return false;
        }

        value = (int)number;

        return true;
    }

    // A missing key is false; anything but true or false refuses the effect.
    private static bool TryReadFlag(LuaTable? table, string key, out bool value)
    {
        value = false;

        if (table is null || table[key].Type == LuaValueType.Nil)
        {
            return true;
        }

        return table[key].Type == LuaValueType.Boolean && table[key].TryRead(out value);
    }
}
