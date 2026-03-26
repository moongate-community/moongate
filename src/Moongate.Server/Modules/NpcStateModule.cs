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
    private const string AiActionKey = "ai_action";
    private const string AiTargetSerialKey = "ai_target_serial";
    private const string LegacyActionKey = "modernuo_action";
    private const string LegacyTargetSerialKey = "modernuo_target_serial";

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

        if (!TryGetBlackboardValue(npc!, key.Trim(), out var value))
        {
            return null;
        }

        return value;
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
            return RemoveBlackboardValue(npc!, normalizedKey);
        }

        return SetBlackboardValue(npc!, normalizedKey, value);
    }

    private static object? ConvertDynValue(DynValue value)
        => value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean              => value.Boolean,
            DataType.Number               => value.Number,
            DataType.String               => value.String,
            _                             => value.ToString()
        };

    private static object? ConvertCustomPropertyValue(ItemCustomProperty property)
        => property.Type switch
        {
            ItemCustomPropertyType.Integer => property.IntegerValue,
            ItemCustomPropertyType.Boolean => property.BooleanValue,
            ItemCustomPropertyType.Double  => property.DoubleValue,
            ItemCustomPropertyType.String  => property.StringValue,
            _                              => null
        };

    private static bool TryGetLegacyAliasKey(string key, out string legacyKey)
    {
        legacyKey = key switch
        {
            AiActionKey        => LegacyActionKey,
            AiTargetSerialKey  => LegacyTargetSerialKey,
            _                  => string.Empty
        };

        return legacyKey.Length > 0;
    }

    private static bool TryReadBlackboardValue(UOMobileEntity npc, string key, out object? value)
    {
        if (!npc.CustomProperties.TryGetValue(key, out var property))
        {
            value = null;

            return false;
        }

        value = ConvertCustomPropertyValue(property);

        return true;
    }

    private bool TryGetBlackboardValue(UOMobileEntity npc, string key, out object? value)
    {
        if (TryReadBlackboardValue(npc, key, out value))
        {
            ClearLegacyAlias(npc, key);

            return true;
        }

        if (!TryGetLegacyAliasKey(key, out var legacyKey) || !TryReadBlackboardValue(npc, legacyKey, out var legacyValue))
        {
            value = null;

            return false;
        }

        npc.SetCustomProperty(key, npc.CustomProperties[legacyKey]);
        npc.RemoveCustomProperty(legacyKey);
        value = legacyValue;

        return true;
    }

    private bool RemoveBlackboardValue(UOMobileEntity npc, string key)
    {
        var removed = npc.RemoveCustomProperty(key);

        if (TryGetLegacyAliasKey(key, out var legacyKey))
        {
            removed |= npc.RemoveCustomProperty(legacyKey);
        }

        return removed;
    }

    private void ClearLegacyAlias(UOMobileEntity npc, string key)
    {
        if (TryGetLegacyAliasKey(key, out var legacyKey))
        {
            npc.RemoveCustomProperty(legacyKey);
        }
    }

    private bool SetBlackboardValue(UOMobileEntity npc, string key, object? value)
    {
        switch (value)
        {
            case bool boolValue:
                npc.SetCustomBoolean(key, boolValue);

                break;
            case sbyte sbyteValue:
                npc.SetCustomInteger(key, sbyteValue);

                break;
            case byte byteValue:
                npc.SetCustomInteger(key, byteValue);

                break;
            case short shortValue:
                npc.SetCustomInteger(key, shortValue);

                break;
            case ushort ushortValue:
                npc.SetCustomInteger(key, ushortValue);

                break;
            case int intValue:
                npc.SetCustomInteger(key, intValue);

                break;
            case uint uintValue:
                npc.SetCustomInteger(key, uintValue);

                break;
            case long longValue:
                npc.SetCustomInteger(key, longValue);

                break;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                npc.SetCustomInteger(key, (long)ulongValue);

                break;
            case float floatValue:
                npc.SetCustomDouble(key, floatValue);

                break;
            case double doubleValue:
                npc.SetCustomDouble(key, doubleValue);

                break;
            case decimal decimalValue:
                npc.SetCustomDouble(key, (double)decimalValue);

                break;
            case string stringValue:
                npc.SetCustomString(key, stringValue);

                break;
            case DynValue dynValue:
                return SetBlackboardValue(npc, key, ConvertDynValue(dynValue));
            default:
                npc.SetCustomString(key, value.ToString());

                break;
        }

        ClearLegacyAlias(npc, key);

        return true;
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
