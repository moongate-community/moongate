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

        var gender = fields.Get("gender");

        if (gender.Type == DataType.String && Enum.TryParse<GenderType>(gender.String, ignoreCase: true, out var g))
        {
            mobile.Gender = g;
        }

        var race = fields.Get("race");

        if (race.Type == DataType.String && Enum.TryParse<RaceType>(race.String, ignoreCase: true, out var r))
        {
            mobile.Race = r;
        }

        var direction = fields.Get("direction");

        if (direction.Type == DataType.String &&
            Enum.TryParse<DirectionType>(direction.String, ignoreCase: true, out var d))
        {
            mobile.Direction = d;
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

    [ScriptFunction("get_skill", "Returns the skill value for the mobile, or 0.")]
    public int GetSkill(uint serial, int skillId)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        return mobile is null ? 0 : mobile.Skills.GetValueOrDefault(skillId, 0);
    }

    [ScriptFunction("set_skill", "Sets a skill value on the mobile; false on unknown serial.")]
    public bool SetSkill(uint serial, int skillId, int value)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        if (mobile is null)
        {
            return false;
        }

        mobile.Skills[skillId] = value;
        _mobiles.UpsertAsync(mobile).WaitSync();

        return true;
    }

    [ScriptFunction("skills", "Returns the mobile's skill values keyed by skill id, or nil.")]
    public Dictionary<int, int>? Skills(uint serial)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        return mobile is null ? null : new(mobile.Skills);
    }

    [ScriptFunction("delete", "Deletes the mobile; true when it existed.")]
    public bool Delete(uint serial)
        => _mobiles.RemoveAsync((Serial)serial).WaitSync();

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
