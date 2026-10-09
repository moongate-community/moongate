using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Bodies;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Lua;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Data.Death;
using Serilog;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Core.Utils;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Moongate.Server.Ultima.Services;
using Moongate.Server.Ultima.Modules.Internal;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     The <c>mobile</c> Lua module: what a script does to a mobile in the world, a player or an NPC, by serial, such
///     as
///     the teleporter that moves whoever walks onto it; <c>mobile.teleport(who, x, y, z)</c>. A serial that is not a
///     mobile in the world gives <c>false</c> or <c>nil</c>, never an error.
/// </summary>
[ScriptModule(
    "mobile",
    "Acts on a mobile in the world, a player or an NPC: teleports it, reads where it is, plays a sound on it, tells a player something."
)]
public sealed class MobileModule
{
    private const double TenthsPerPoint = 10.0;
    private const int MaximumStealthSteps = 1000;

    private readonly IMobileService _mobiles;
    private readonly ITeleportService _teleports;
    private readonly ISpeechService _speech;
    private readonly IItemService? _items;
    private readonly IMusicService? _music;
    private readonly IRegionService? _regions;
    private readonly ILightService? _light;
    private readonly IMobileStateService? _state;
    private readonly IWorldViewService? _view;
    private readonly Lazy<FrozenDictionary<int, BodyType>> _bodies;
    private readonly IWeightService? _weight;
    private readonly ICrimeService? _crimes;
    private readonly IDeathService? _death;
    private readonly IMountService? _mounts;

    private readonly ISessionService? _sessions;
    private readonly IPacketSendService? _sender;

    public MobileModule(
        IMobileService mobiles,
        ITeleportService teleports,
        ISpeechService speech,
        IItemService? items = null,
        IMusicService? music = null,
        IRegionService? regions = null,
        ILightService? light = null,
        IMobileStateService? state = null,
        IWorldViewService? view = null,
        IDataLoaderService? data = null,
        IWeightService? weight = null,
        ICrimeService? crimes = null,
        IDeathService? death = null,
        ISessionService? sessions = null,
        IPacketSendService? sender = null,
        IMountService? mounts = null
    )
    {
        _mounts = mounts;
        _sessions = sessions;
        _sender = sender;
        _death = death;
        _crimes = crimes;
        _weight = weight;
        _bodies = new(() =>
            data?.GetEntities<BodyContent>().ToFrozenDictionary(body => (int)body.Body.Value, body => body.Type) ??
            FrozenDictionary<int, BodyType>.Empty
        );
        _view = view;
        _state = state;
        _mobiles = mobiles;
        _teleports = teleports;
        _speech = speech;
        _items = items;
        _music = music;
        _regions = regions;
        _light = light;
    }

    /// <summary>
    ///     Gets the id of the template an NPC was made from, such as to tell a baker from a blacksmith;
    ///     <c>mobile.template(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The id of the mobile template an NPC was made from, such as f_baker; nil for a player or a mobile not in the world."
    )]
    public string? Template(long serial)
    {
        return TryGetMobile(serial, out var mobile) && !string.IsNullOrEmpty(mobile.TemplateId) ? mobile.TemplateId : null;
    }

    /// <summary>
    ///     Gets the notoriety the mobile is shown with; <c>mobile.notoriety(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The notoriety others see the mobile with, as a name: innocent (blue), ally, attackable, criminal (grey), enemy, murderer (red) or invulnerable (yellow). A criminal reads criminal and a murderer murderer, whatever its template says. nil for a mobile not in the world."
    )]
    public string? Notoriety(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? EnumNameUtils.Format(mobile.ShownNotoriety) : null;
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
    ///     Gets how many steps a hidden player may still take unseen; <c>mobile.stealth_steps(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "How many more steps the mobile may take hidden before a step shows it, as the Stealth skill gave them; 0 for none, nil for a mobile not in the world."
    )]
    public int? StealthSteps(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? mobile.AllowedStealthSteps : null;
    }

    /// <summary>
    ///     Sets how many steps a hidden player may take unseen; <c>mobile.set_stealth_steps(who, 8)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets how many steps the hidden mobile may take before a step shows it (0 to 1000); hiding or showing it again clears them, and running always shows it. False for a mobile not in the world or a number out of range."
    )]
    public bool SetStealthSteps(long serial, int steps)
    {
        if (steps is < 0 or > MaximumStealthSteps || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        mobile.AllowedStealthSteps = steps;

        return true;
    }

    /// <summary>
    ///     Gets whether the mobile is female; <c>mobile.is_female(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText: "Whether the mobile, a player or an NPC, is female; false for a male one and for an unknown serial."
    )]
    public bool IsFemale(long serial)
    {
        return TryGetMobile(serial, out var mobile) && mobile.Gender == GenderType.Female;
    }

    /// <summary>
    ///     Gets whether the serial is a player's character in the world; <c>mobile.is_player(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText: "Whether the serial is a player's character in the world; false for an NPC or an unknown serial."
    )]
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
    [ScriptFunction(
        helpText:
        "The mobile's numbers as a table: body, strength, dexterity, intelligence, hits, hits_max, mana, mana_max, stamina, stamina_max, fame, karma; nil for a mobile not in the world. Read only; change them with mobile.set_stats."
    )]
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
    ///     Changes the mobile's numbers: any of those <see cref="Stats" /> gives but the body;
    ///     <c>mobile.set_stats(who, { hits = 10, strength = 80 })</c>. Hit points, mana and stamina stay between 0 and
    ///     their maximum. The mobile's player sees the new status and the players around the new health bar.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Changes the mobile's numbers, given as a table with any of those mobile.stats gives but body; hits, mana and stamina stay between 0 and their maximum, also when only the maximum changes. Its player sees its bars or status change, the players around its health bar. False, with nothing changed, for an unknown name, a value that is not a whole number, a strength, dexterity, intelligence or maximum outside 0 to 65535, an empty table or a mobile not in the world."
    )]
    public bool SetStats(long serial, LuaTable values)
    {
        if (_state is null || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        var change = new MobileStatsChange();
        var given = 0;

        foreach (var (key, value) in values)
        {
            if (!key.TryRead<string>(out var name) ||
                value.Type != LuaValueType.Number ||
                !value.TryRead<double>(out var number) ||
                Math.Floor(number) != number ||
                number is < int.MinValue or > int.MaxValue)
            {
                return false;
            }

            var whole = (int)number;

            switch (name)
            {
                case "strength":
                    change.Strength = whole;

                    break;
                case "dexterity":
                    change.Dexterity = whole;

                    break;
                case "intelligence":
                    change.Intelligence = whole;

                    break;
                case "hits":
                    change.Hits = whole;

                    break;
                case "hits_max":
                    change.HitsMax = whole;

                    break;
                case "mana":
                    change.Mana = whole;

                    break;
                case "mana_max":
                    change.ManaMax = whole;

                    break;
                case "stamina":
                    change.Stamina = whole;

                    break;
                case "stamina_max":
                    change.StaminaMax = whole;

                    break;
                case "fame":
                    change.Fame = whole;

                    break;
                case "karma":
                    change.Karma = whole;

                    break;
                default:
                    return false;
            }

            given++;
        }

        return given > 0 && _state.SetStats(mobile, change);
    }

    /// <summary>
    ///     Gets a skill of the mobile, in points: <c>mobile.skill(who, SkillType.Magery).value</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "A skill of the mobile as { value, cap, lock }: value and cap in points (50.5), lock is up, down or locked; a skill never trained is 0. nil for a mobile not in the world."
    )]
    public LuaTable? Skill(long serial, SkillType skill)
    {
        if (_state is null || !TryGetMobile(serial, out var mobile))
        {
            return null;
        }

        var known = _state.GetSkill(mobile, skill);
        var table = new LuaTable();
        table["value"] = known.Base / TenthsPerPoint;
        table["cap"] = known.Cap / TenthsPerPoint;
        table["lock"] = EnumNameUtils.Format(known.Lock);

        return table;
    }

    /// <summary>
    ///     Gets the skills the mobile has above 0, by name, in points; <c>mobile.skills(who).magery</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The skills of the mobile above 0, as a table of name (magery, evaluating_intelligence) and value in points; nil for a mobile not in the world."
    )]
    public LuaTable? Skills(long serial)
    {
        if (_state is null || !TryGetMobile(serial, out var mobile))
        {
            return null;
        }

        var table = new LuaTable();

        foreach (var skill in _state.GetSkills(mobile).Where(skill => skill.Base > 0))
        {
            table[EnumNameUtils.Format(skill.Skill)] = skill.Base / TenthsPerPoint;
        }

        return table;
    }

    /// <summary>
    ///     Sets a skill of the mobile, in points, and its cap when given;
    ///     <c>mobile.set_skill(who, SkillType.Magery, 50.5)</c>. The value stays between 0 and the cap, and the
    ///     mobile's player sees it in the skill window.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets a skill of the mobile in points (50.5), and its cap when given; the value stays between 0 and the cap. False for a cap outside 0 to 6553.5 or a mobile not in the world; a skill number that is no SkillType raises an error. The mobile's player sees it in the skill window."
    )]
    public bool SetSkill(long serial, SkillType skill, double value, double? cap = null)
    {
        return _state is not null &&
               TryGetMobile(serial, out var mobile) &&
               Tenths(value) is { } tenths &&
               (cap is null || Tenths(cap.Value) is not null) &&
               _state.SetSkill(mobile, skill, tenths, cap is null ? null : Tenths(cap.Value));
    }

    /// <summary>
    ///     Gives the mobile another name; <c>mobile.set_name(who, "Grog")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Gives the mobile another name of at most 30 characters, seen by its player and the players around; false for a blank or longer name or a mobile not in the world."
    )]
    public bool SetName(long serial, string name)
    {
        return _state is not null && TryGetMobile(serial, out var mobile) && _state.SetName(mobile, name);
    }

    /// <summary>
    ///     Gives the mobile another body, such as an animal's; <c>mobile.set_body(who, 0xD3)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Gives the mobile another body graphic (0 to 65535), seen at once by its player and the players around; false for a body out of range or a mobile not in the world."
    )]
    public bool SetBody(long serial, int body)
    {
        return _state is not null && TryGetMobile(serial, out var mobile) && _state.SetLooks(mobile, body, null);
    }

    /// <summary>
    ///     Gives the mobile another skin hue; <c>mobile.set_hue(who, 1153)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Gives the mobile another skin hue (0 to 65535, 0 the colours of its art), seen at once by its player and the players around; false for a hue out of range or a mobile not in the world."
    )]
    public bool SetHue(long serial, int hue)
    {
        return _state is not null && TryGetMobile(serial, out var mobile) && _state.SetLooks(mobile, null, hue);
    }

    /// <summary>
    ///     Gets what the mobile is as a table of booleans; <c>mobile.flags(who).hidden</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "What the mobile is, as { hidden, frozen, war_mode }, each true or false; nil for a mobile not in the world."
    )]
    public LuaTable? Flags(long serial)
    {
        if (!TryGetMobile(serial, out var mobile))
        {
            return null;
        }

        var table = new LuaTable();
        table["hidden"] = mobile.Hidden;
        table["frozen"] = mobile.Frozen;
        table["war_mode"] = mobile.WarMode;

        return table;
    }

    /// <summary>
    ///     Hides or reveals the mobile; <c>mobile.set_hidden(who, true)</c>. Hidden, it leaves the screens of the
    ///     players around; the staff still sees it.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Hides or reveals the mobile: hidden, the players around no longer see it, hear it, open its paperdoll or read its tooltip, and NPCs do not sense it; the staff still sees it and world.mobiles_in_range still returns it. Saved with the mobile. False for a mobile not in the world."
    )]
    public bool SetHidden(long serial, bool hidden)
    {
        if (_state is null || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        _state.SetHidden(mobile, hidden);

        return true;
    }

    /// <summary>
    ///     Freezes or frees the mobile; <c>mobile.set_frozen(who, true)</c>. Frozen, it neither steps nor turns.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Freezes or frees the mobile: frozen, it neither steps nor turns; saved with the mobile. False for a mobile not in the world."
    )]
    public bool SetFrozen(long serial, bool frozen)
    {
        if (_state is null || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        _state.SetFrozen(mobile, frozen);

        return true;
    }

    /// <summary>
    ///     Puts the mobile in war or peace mode; <c>mobile.set_war_mode(who, false)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Puts the mobile in war mode or in peace, seen by its player and the players around; a mobile comes back in peace after a restart. False for a mobile not in the world."
    )]
    public bool SetWarMode(long serial, bool warMode)
    {
        if (_state is null || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        _state.SetWarMode(mobile, warMode);

        return true;
    }

    /// <summary>
    ///     Gets the backpack the mobile wears, to look into it with <c>item.contents</c>; <c>mobile.backpack(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The serial of the backpack the mobile wears, to look into with item.contents; nil for a mobile without one or not in the world."
    )]
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
    [ScriptFunction(
        helpText: "The name of the region the mobile stands in; nil outside every region or for a mobile not in the world."
    )]
    public string? Region(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? _regions?.Find(mobile.Map, mobile.Location)?.Name : null;
    }

    /// <summary>
    ///     Gets the light level where the mobile stands, 0 the brightest day and 30 the darkest;
    ///     <c>mobile.light(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The light level where the mobile stands, 0 (day) to 30 (dark): the hour, or its dungeon's; nil for a mobile not in the world."
    )]
    public int? Light(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? _light?.LevelFor(mobile) : null;
    }

    /// <summary>
    ///     Plays a music to a player, until its region gives it another;
    ///     <c>mobile.play_music(who, MusicType.Britain1)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Plays a music (a MusicType) to the player, until its region gives it another; false for an NPC or a player not in the world."
    )]
    public bool PlayMusic(long serial, MusicType music)
    {
        return _music is not null && TryGetMobile(serial, out var mobile) && !mobile.IsNpc && _music.Play(mobile, music);
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> the mobile keeps, saved with it across restarts;
    ///     <c>mobile.get_prop(who, "quest.step")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "A value the mobile, a player or an NPC, keeps across restarts: a string, a number or a bool; nil when it has none."
    )]
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
    [ScriptFunction(
        helpText:
        "Keeps a string, a number or a bool on the mobile across restarts, a player's saved with its character; nil removes it. False for a table, a function, a blank key or a mobile not in the world."
    )]
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
    ///     <c>mobile.teleport(who, 259, 785, 64, MapType.Tokuno)</c>. A player's client is told where it stands, after
    ///     the
    ///     map change when there is one; the players around the old spot lose the mobile and those around the new one
    ///     see it.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Teleports the mobile to x, y, z on its map, or on the given map (a MapType or its name, such as Tokuno); the players around the old spot lose it and those around the new one see it. False for a mobile not in the world, a map that does not exist or is not loaded, a spot outside the map or a z outside -128 to 127."
    )]
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
            _                                                                                             => null
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
    ///     Plays an animation of the mobile, seen by its player and those around; <c>mobile.animate(who, 32)</c> makes
    ///     a
    ///     human bow. The action is a number of the mobile's body: a human and a monster do not share them.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Plays an action (0 to 65535) of the mobile's body, seen by its player and those who see it, with frames (1 to 255, default 5) and how many times (1 to 255, default 1). The bodies do not share the numbers: use the names of HumanAnimationType, MonsterAnimationType or AnimalAnimationType. False for a mobile not in the world or a number out of range."
    )]
    public bool Animate(long serial, int action, int frames = 5, int repeatCount = 1)
    {
        if (_view is null ||
            action is < 0 or > ushort.MaxValue ||
            frames is < 1 or > byte.MaxValue ||
            repeatCount is < 1 or > byte.MaxValue ||
            !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        _view.MobileAnimated(mobile, action, frames, repeatCount);

        return true;
    }

    /// <summary>
    ///     Plays <paramref name="sound" /> where the mobile stands for the players within 15 cells;
    ///     <c>mobile.play_sound(who, 0x1FE)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Plays a sound id (0 to 65535) where the mobile stands, for the players within 15 cells; false for a mobile not in the world or a sound out of range."
    )]
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
    ///     Tells the client of a player to walk its character to a spot;
    ///     <c>mobile.pathfind_to(who, 1500, 1600, 10)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Tells the player's client to walk its character to x, y, z of its map by itself, as after a double right click on the ground: the client finds the way and walks it step by step, and the player may walk elsewhere at any time. It is the client that decides: a spot too far or with no way to it is simply not walked to, and the server is not told. True when the client was told; false for an NPC, a player that is not in the world, or coordinates outside 0 to 65535 (x, y) and -128 to 127 (z)."
    )]
    public bool PathfindTo(long serial, double x, double y, double z)
    {
        if (_sessions is null ||
            _sender is null ||
            !TryGetMobile(serial, out var mobile) ||
            x is < 0 or > ushort.MaxValue ||
            y is < 0 or > ushort.MaxValue ||
            z is < sbyte.MinValue or > sbyte.MaxValue ||
            Math.Floor(x) != x ||
            Math.Floor(y) != y ||
            Math.Floor(z) != z ||
            !_sessions.TryGetByCharacterId(mobile.Id, out var session))
        {
            return false;
        }

        return _sender.TrySend(session.SessionId, new PathfindPacket(new Point3D((int)x, (int)y, (int)z)));
    }

    /// <summary>
    ///     Sends <paramref name="text" /> to the player as a system message, in the lower left of its screen;
    ///     <c>mobile.message(who, "That is too far away.")</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "A system message, in the lower left of the screen, read only by that player (cut at 128 characters); false for an empty text, an NPC or a player not in the world."
    )]
    public bool Message(long serial, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        return _speech.Tell(
            mobile,
            text.Length > ItemModule.MaximumTextLength ? text[..ItemModule.MaximumTextLength] : text
        );
    }

    /// <summary>
    ///     Sends a text of the client, by its number, to the player as a system message, in the language of that
    ///     client; <c>mobile.message_cliloc(who, 500867)</c>. The arguments fill its <c>~1_NAME~</c> places, split by
    ///     tabs.
    /// </summary>
    [ScriptFunction(
        helpText:
        "A system message of the client's own texts, by cliloc number, read only by that player in the language of its client; args fills its ~1_NAME~ places, split by tabs. False for a number not above 0, an NPC or a player not in the world."
    )]
    public bool MessageCliloc(long serial, int cliloc, string? args = null)
    {
        return cliloc > 0 && TryGetMobile(serial, out var mobile) && _speech.TellCliloc(mobile, cliloc, args ?? "");
    }

    /// <summary>
    ///     Gets the kind of body of the mobile, as <c>data/bodies.toml</c> says:
    ///     <c>mobile.body_type(who) == BodyType.Human</c>. The kind tells which animations the body has.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The kind of the mobile's body, as data/bodies.toml says, a BodyType: Human (every race of player and the human NPCs), Monster, Animal, Sea, Equipment, or Empty for a body the file does not list; it tells which animations the body has. Nil for a mobile not in the world."
    )]
    public BodyType? BodyType(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? _bodies.Value.GetValueOrDefault(mobile.Body) : null;
    }

    /// <summary>
    ///     Gets how full the mobile is, from 0 (starving) to 20 (full); <c>mobile.hunger(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "How full the mobile is, from 0 (starving) to 20 (full); a player loses a point every ultima.regeneration.hunger_minutes, counted from when it entered the world, and at 0 gets no hit points back; the staff neither gets hungry nor starves. Nil for a mobile not in the world."
    )]
    public int? Hunger(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? mobile.Hunger : null;
    }

    /// <summary>
    ///     Sets how full the mobile is, kept from 0 to 20; <c>mobile.set_hunger(who, mobile.hunger(who) + 3)</c>. A
    ///     player with 0 gets no hit points back.
    /// </summary>
    [ScriptFunction(
        helpText: "Sets how full the mobile is, kept from 0 (starving) to 20 (full); false for a mobile not in the world."
    )]
    public bool SetHunger(long serial, int hunger)
    {
        if (!TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        mobile.Hunger = HungerService.Clamp(hunger);

        return true;
    }

    /// <summary>
    ///     Gets how quenched the mobile is, from 0 (parched) to 20 (quenched); <c>mobile.thirst(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "How quenched the mobile is, from 0 (parched) to 20 (quenched); a player loses a point every ultima.regeneration.hunger_minutes, counted as for hunger, and at 0 gets no stamina back; the staff is left alone. Nil for a mobile not in the world."
    )]
    public int? Thirst(long serial)
    {
        return TryGetMobile(serial, out var mobile) ? mobile.Thirst : null;
    }

    /// <summary>
    ///     Sets how quenched the mobile is, kept from 0 to 20; <c>mobile.set_thirst(who, mobile.thirst(who) + 3)</c>.
    ///     A player with 0 gets no stamina back.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Sets how quenched the mobile is, kept from 0 (parched) to 20 (quenched); false for a mobile not in the world."
    )]
    public bool SetThirst(long serial, int thirst)
    {
        if (!TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        mobile.Thirst = HungerService.Clamp(thirst);

        return true;
    }

    /// <summary>
    ///     Gets the stones the mobile carries: what it wears with their contents, without the bank;
    ///     <c>mobile.weight(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The stones the mobile carries: what it wears and everything inside, without the bank box, each pile rounded up as its tooltip says; nil for a mobile not in the world."
    )]
    public int? Weight(long serial)
    {
        return _weight is not null && TryGetMobile(serial, out var mobile) ? _weight.Carried(mobile) : null;
    }

    /// <summary>
    ///     Gets the stones the mobile may carry before it is overloaded, 40 and three and a half a point of strength;
    ///     <c>if mobile.weight(who) > mobile.max_weight(who) then ... end</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The stones the mobile may carry before it is overloaded: 40 and 3.5 a point of strength; nil for a mobile not in the world."
    )]
    public int? MaxWeight(long serial)
    {
        return _weight is not null && TryGetMobile(serial, out var mobile) ? _weight.MaxCarried(mobile) : null;
    }

    /// <summary>
    ///     Gets whether the mobile is a criminal now, its name in grey; <c>mobile.criminal(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Whether the mobile is a criminal now, its name in grey for those who see it; nil for a mobile not in the world."
    )]
    public bool? Criminal(long serial)
    {
        return _crimes is not null && TryGetMobile(serial, out var mobile) ? _crimes.IsCriminal(mobile) : null;
    }

    /// <summary>
    ///     Kills an NPC, which dies where it stands and leaves its corpse; <c>mobile.kill(orc, user)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Kills a mobile: it leaves its corpse with what it carried and wore and the players around see and hear it die. An NPC runs its on_death(npc, corpse, killer) and leaves the world; a player stays as a ghost. killer is the serial of who did it, kept on the corpse, or nil. False for a mobile not in the world, a player that is dead already, or a body without a ghost."
    )]
    public bool Kill(long serial, long? killer = null)
    {
        if (_death is null || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        MobileEntity? by = null;

        if (killer is { } id)
        {
            // Safe: the out value is only used when the lookup succeeds.
            TryGetMobile(id, out by!);
        }

        return _death.Kill(mobile, by);
    }

    /// <summary>
    ///     Tells whether the mobile is a dead player, a ghost; <c>mobile.is_dead(serial)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "True for a player that is dead, a ghost; false for anyone else, an NPC that dies leaves the world, or a serial that is not a mobile."
    )]
    public bool IsDead(long serial)
    {
        return TryGetMobile(serial, out var mobile) && mobile.IsDead;
    }

    /// <summary>
    ///     Gets whether the mobile sits on a mount; <c>mobile.is_mounted(who)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "True when the mobile rides a mount, such as a horse; false for a mobile on foot or a serial that is not a mobile in the world."
    )]
    public bool IsMounted(long serial)
    {
        return _mounts is not null && TryGetMobile(serial, out var mobile) && _mounts.IsMounted(mobile);
    }

    /// <summary>
    ///     Gets the murder counts of a player; <c>mobile.murders(serial).short_term</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "The murder counts of the mobile as { kills, short_term }: the kills a victim reported (five make a murderer, red) and the short-term murders (five make a resurrection cost skills and stats). nil for a mobile not in the world."
    )]
    public LuaTable? Murders(long serial)
    {
        if (!TryGetMobile(serial, out var mobile))
        {
            return null;
        }

        var table = new LuaTable();
        table["kills"] = mobile.Kills;
        table["short_term"] = mobile.ShortTermMurders;

        return table;
    }

    /// <summary>
    ///     Tells whether the mobile is a murderer; <c>mobile.is_murderer(serial)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "True for a player with five kills or more reported against it, a murderer with a red name; false for anyone else or a serial that is not a mobile."
    )]
    public bool IsMurderer(long serial)
    {
        return TryGetMobile(serial, out var mobile) && mobile.IsMurderer;
    }

    /// <summary>
    ///     Raises who died from its corpse, or a ghost where it stands; <c>mobile.resurrect(corpse)</c>.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Raises a dead player, a ghost, at once: its living body is back with 10 hit points, full stamina and no mana, and a death robe replaces its shroud; false for a player that is not dead. Given the corpse of an NPC instead, raises the NPC a corpse is of, on a later turn of the game loop: one of the same template is born where the corpse lies, with the name and the facing of who died and the equipment of its template, and the corpse is gone with what was left inside. False, and nothing is started, for what is not an item with the corpse graphic; a corpse that names no template is left as it is."
    )]
    public bool Resurrect(long corpse)
    {
        if (_death is not null && TryGetMobile(corpse, out var ghost))
        {
            return _death.Resurrect(ghost);
        }

        if (_death is null ||
            _items is null ||
            corpse is <= 0 or > uint.MaxValue ||
            !_items.TryGet(new Serial((uint)corpse), out var item) ||
            item.ItemId != CorpseProps.Graphic)
        {
            return false;
        }

        // The birth of an NPC waits for the database: a script runs on the game loop, so it is started off it.
        var serial = item.Id;
        _ = Task.Run(() => _death.ResurrectAsync(serial))
            .ContinueWith(
                task => Log.ForContext<MobileModule>()
                    .Warning(task.Exception, "mobile.resurrect of {Serial} failed", serial),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default
            );

        return true;
    }

    /// <summary>
    ///     Makes the mobile a criminal for the configured time, or pardons it at once;
    ///     <c>mobile.set_criminal(thief, true)</c>. Making one again starts the time again.
    /// </summary>
    [ScriptFunction(
        helpText:
        "Makes the mobile a criminal for ultima.crime.criminal_seconds, starting the time again if it is one, or pardons it at once with false; false for a mobile not in the world."
    )]
    public bool SetCriminal(long serial, bool criminal)
    {
        if (_crimes is null || !TryGetMobile(serial, out var mobile))
        {
            return false;
        }

        if (criminal)
        {
            _crimes.MakeCriminal(mobile);
        }
        else
        {
            _crimes.Pardon(mobile);
        }

        return true;
    }

    // Points to the tenths the mobile keeps; null for a value no skill can have.
    private static int? Tenths(double points)
    {
        var tenths = Math.Round(points * TenthsPerPoint, MidpointRounding.AwayFromZero);

        return double.IsFinite(tenths) && tenths is >= int.MinValue and <= int.MaxValue ? (int)tenths : null;
    }

    private bool TryGetMobile(long serial, [NotNullWhen(true)] out MobileEntity? mobile)
    {
        mobile = null;

        return serial is > 0 and <= uint.MaxValue && _mobiles.TryGet(new Serial((uint)serial), out mobile);
    }
}
