using System.Globalization;
using MoonSharp.Interpreter;
using Moongate.Core.Extensions;
using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Entities;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Exposes mobile creation and manipulation to Lua. Mobiles are referenced by serial (a number).
/// All functions are synchronous and must be called on the game-loop thread (from
/// <c>game.post</c> / <c>game.schedule</c>), the single-writer boundary for mobile state.
/// </summary>
[ScriptModule("mobile", "Create and manipulate mobiles by serial.")]
public sealed class MobileModule
{
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;

    public MobileModule(IPersistenceService persistence)
    {
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
    }

    [ScriptFunction("create", "Creates a mobile at a location; returns its serial.")]
    public uint? Create(string name, int map, int x, int y, int z)
    {
        var mobile = new MobileEntity { Name = name, MapId = map, Position = new Point3D(x, y, z) };
        _mobiles.UpsertAsync(mobile).WaitSync();

        return mobile.Id.Value;
    }

    [ScriptFunction("get", "Returns a field table for the mobile, or nil.")]
    public Dictionary<string, object?>? Get(uint serial)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        if (mobile is null)
        {
            return null;
        }

        return new()
        {
            ["id"] = mobile.Id.Value,
            ["name"] = mobile.Name,
            ["map"] = mobile.MapId,
            ["x"] = mobile.Position.X,
            ["y"] = mobile.Position.Y,
            ["z"] = mobile.Position.Z,
            ["direction"] = mobile.Direction.ToString(),
            ["gender"] = mobile.Gender.ToString(),
            ["race"] = mobile.Race.ToString(),
            ["profession"] = (int)mobile.ProfessionId,
            ["str"] = mobile.Strength,
            ["dex"] = mobile.Dexterity,
            ["int"] = mobile.Intelligence,
            ["backpack"] = mobile.BackpackId.Value
        };
    }

    [ScriptFunction("set", "Mutates mobile fields from a table; returns true on success.")]
    public bool Set(uint serial, Table fields)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        if (mobile is null || fields is null)
        {
            return false;
        }

        var name = fields.Get("name");

        if (name.Type == DataType.String)
        {
            mobile.Name = name.String;
        }

        ApplyInt(fields, "str", value => mobile.Strength = value);
        ApplyInt(fields, "dex", value => mobile.Dexterity = value);
        ApplyInt(fields, "int", value => mobile.Intelligence = value);
        ApplyInt(fields, "profession", value => mobile.ProfessionId = (byte)value);
        ApplyInt(fields, "map", value => mobile.MapId = value);
        ApplyInt(fields, "hair_style", value => mobile.HairStyle = (ushort)value);
        ApplyInt(fields, "facial_hair_style", value => mobile.FacialHairStyle = (ushort)value);
        ApplyHue(fields, "skin_hue", value => mobile.SkinHue = value);
        ApplyHue(fields, "hair_hue", value => mobile.HairHue = value);
        ApplyHue(fields, "facial_hair_hue", value => mobile.FacialHairHue = value);

        if (ScriptEnums.TryResolve<GenderType>(fields.Get("gender"), out var gender))
        {
            mobile.Gender = gender;
        }

        if (ScriptEnums.TryResolve<RaceType>(fields.Get("race"), out var race))
        {
            mobile.Race = race;
        }

        if (ScriptEnums.TryResolve<DirectionType>(fields.Get("direction"), out var direction))
        {
            mobile.Direction = direction;
        }

        _mobiles.UpsertAsync(mobile).WaitSync();

        return true;
    }

    [ScriptFunction("move", "Moves the mobile to (x, y, z) on the same map; false on unknown serial.")]
    public bool Move(uint serial, int x, int y, int z)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        if (mobile is null)
        {
            return false;
        }

        mobile.Position = new Point3D(x, y, z);
        _mobiles.UpsertAsync(mobile).WaitSync();

        return true;
    }

    [ScriptFunction("get_skill", "Returns the skill value for the mobile by skill name or id, or 0.")]
    public int GetSkill(uint serial, object skill)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        if (mobile is null || !TryResolveSkill(skill, out var skillId))
        {
            return 0;
        }

        return mobile.Skills.GetValueOrDefault(skillId, 0);
    }

    [ScriptFunction("set_skill", "Sets a skill value on the mobile by skill name or id; false on unknown serial/skill.")]
    public bool SetSkill(uint serial, object skill, int value)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        if (mobile is null || !TryResolveSkill(skill, out var skillId))
        {
            return false;
        }

        mobile.Skills[skillId] = value;
        _mobiles.UpsertAsync(mobile).WaitSync();

        return true;
    }

    [ScriptFunction("skills", "Returns the mobile's skill values keyed by skill name, or nil.")]
    public Dictionary<string, int>? Skills(uint serial)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        if (mobile is null)
        {
            return null;
        }

        var skills = new Dictionary<string, int>();

        foreach (var (skillId, value) in mobile.Skills)
        {
            skills[((SkillName)skillId).ToString()] = value;
        }

        return skills;
    }

    [ScriptFunction("delete", "Deletes the mobile; true when it existed.")]
    public bool Delete(uint serial)
        => _mobiles.RemoveAsync((Serial)serial).WaitSync();

    private static bool TryResolveSkill(object? skill, out int skillId)
    {
        skillId = 0;

        switch (skill)
        {
            // A skill name, in display form ("Animal Lore") or compact form ("AnimalLore").
            case string name when !string.IsNullOrWhiteSpace(name):
            {
                var token = new string(name.Where(char.IsLetter).ToArray());

                if (!Enum.TryParse<SkillName>(token, ignoreCase: true, out var parsed))
                {
                    return false;
                }

                skillId = (int)parsed;

                return true;
            }

            // A numeric skill id, e.g. the value of an exposed SkillName enum constant.
            case IConvertible convertible when skill is not string:
            {
                skillId = convertible.ToInt32(CultureInfo.InvariantCulture);

                return skillId >= 0;
            }

            default:
                return false;
        }
    }

    private static void ApplyInt(Table fields, string key, Action<int> apply)
    {
        var value = fields.Get(key);

        if (value.Type == DataType.Number)
        {
            apply((int)value.Number);
        }
    }

    private static void ApplyHue(Table fields, string key, Action<Hue> apply)
    {
        var value = fields.Get(key);

        if (value.Type == DataType.Number)
        {
            apply(new Hue((ushort)value.Number));
        }
    }
}
