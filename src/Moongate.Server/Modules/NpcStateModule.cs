using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Modules.Internal;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using MoonSharp.Interpreter;

namespace Moongate.Server.Modules;

[ScriptModule("npc_state", "Provides npc state and blackboard helpers.")]

/// <summary>
/// Exposes mutable npc blackboard helpers and hp/alive state to Lua brains.
/// </summary>
public sealed class NpcStateModule
{
    private readonly ISpatialWorldService _spatialWorldService;

    public NpcStateModule(ISpatialWorldService spatialWorldService)
    {
        _spatialWorldService = spatialWorldService;
    }

    [ScriptFunction("get_hp_percent", "Returns current hp percentage [0.0..1.0] for the npc.")]
    public double GetHpPercent(uint npcSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc) || npc!.MaxHits <= 0)
        {
            return 0;
        }

        return Math.Clamp((double)npc.Hits / npc.MaxHits, 0, 1);
    }

    [ScriptFunction("get_var", "Returns a blackboard value for key, or nil.")]
    public object? GetVar(uint npcSerial, string key)
    {
        if (!TryResolveValidNpc(npcSerial, key, out var npc))
        {
            return null;
        }

        if (!npc!.CustomProperties.TryGetValue(key.Trim(), out var property))
        {
            return null;
        }

        return property.Type switch
        {
            ItemCustomPropertyType.Integer => property.IntegerValue,
            ItemCustomPropertyType.Boolean => property.BooleanValue,
            ItemCustomPropertyType.Double  => property.DoubleValue,
            ItemCustomPropertyType.String  => property.StringValue,
            _                              => null
        };
    }

    [ScriptFunction("is_alive", "Returns true when the npc is alive.")]
    public bool IsAlive(uint npcSerial)
    {
        if (!MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out var npc))
        {
            return false;
        }

        return npc!.IsAlive;
    }

    [ScriptFunction("set_var", "Sets a blackboard value for key. Nil value removes the key.")]
    public bool SetVar(uint npcSerial, string key, object? value)
    {
        if (!TryResolveValidNpc(npcSerial, key, out var npc))
        {
            return false;
        }

        var normalizedKey = key.Trim();

        if (value is null || value is DynValue { Type: DataType.Nil or DataType.Void })
        {
            return npc!.RemoveCustomProperty(normalizedKey);
        }

        switch (value)
        {
            case bool boolValue:
                npc!.SetCustomBoolean(normalizedKey, boolValue);

                return true;
            case sbyte sbyteValue:
                npc!.SetCustomInteger(normalizedKey, sbyteValue);

                return true;
            case byte byteValue:
                npc!.SetCustomInteger(normalizedKey, byteValue);

                return true;
            case short shortValue:
                npc!.SetCustomInteger(normalizedKey, shortValue);

                return true;
            case ushort ushortValue:
                npc!.SetCustomInteger(normalizedKey, ushortValue);

                return true;
            case int intValue:
                npc!.SetCustomInteger(normalizedKey, intValue);

                return true;
            case uint uintValue:
                npc!.SetCustomInteger(normalizedKey, uintValue);

                return true;
            case long longValue:
                npc!.SetCustomInteger(normalizedKey, longValue);

                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                npc!.SetCustomInteger(normalizedKey, (long)ulongValue);

                return true;
            case float floatValue:
                npc!.SetCustomDouble(normalizedKey, floatValue);

                return true;
            case double doubleValue:
                npc!.SetCustomDouble(normalizedKey, doubleValue);

                return true;
            case decimal decimalValue:
                npc!.SetCustomDouble(normalizedKey, (double)decimalValue);

                return true;
            case string stringValue:
                npc!.SetCustomString(normalizedKey, stringValue);

                return true;
            case DynValue dynValue:
                return SetVar(npcSerial, normalizedKey, ConvertDynValue(dynValue));
            default:
                npc!.SetCustomString(normalizedKey, value.ToString());

                return true;
        }
    }

    private static object? ConvertDynValue(DynValue value)
    {
        return value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean              => value.Boolean,
            DataType.Number               => value.Number,
            DataType.String               => value.String,
            _                             => value.ToString()
        };
    }

    private bool TryResolveValidNpc(uint npcSerial, string key, out UOMobileEntity? mobile)
    {
        mobile = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return MobileScriptResolver.TryResolveMobile(_spatialWorldService, npcSerial, out mobile);
    }
}
