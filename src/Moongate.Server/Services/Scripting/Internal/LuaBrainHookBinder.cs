using Moongate.Server.Data.Internal.Scripting;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Resolves known hook names from a Lua brain table.
/// </summary>
internal static class LuaBrainHookBinder
{
    public static bool TryBind(Script script, string brainTableName, out LuaBrainResolvedHooks hooks)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentException.ThrowIfNullOrWhiteSpace(brainTableName);

        var table = script.Globals.Get(brainTableName);

        if (table.Type != DataType.Table || table.Table is null)
        {
            hooks = default!;

            return false;
        }

        hooks = new(
            ResolveTableFunction(table, "on_think", "OnThink"),
            ResolveTableFunction(table, "on_speech", "OnSpeech"),
            ResolveTableFunction(table, "on_before_death", "OnBeforeDeath"),
            ResolveTableFunction(table, "on_death", "OnDeath"),
            ResolveTableFunction(table, "on_after_death", "OnAfterDeath"),
            ResolveTableFunction(table, "on_spawn", "OnSpawn"),
            ResolveTableFunction(table, "on_attack", "OnAttack"),
            ResolveTableFunction(table, "on_missed_attack", "OnMissedAttack"),
            ResolveTableFunction(table, "on_attacked", "OnAttacked"),
            ResolveTableFunction(table, "on_missed_by_attack", "OnMissedByAttack"),
            ResolveTableFunction(table, "in_range", "on_in_range", "OnInRange"),
            ResolveTableFunction(table, "out_range", "on_out_range", "OnOutRange"),
            ResolveTableFunction(table, "get_context_menus", "GetContextMenus"),
            ResolveTableFunction(table, "on_selected_context_menu", "OnSelectedContextMenu"),
            ResolveTableFunction(table, "on_event", "OnEvent")
        );

        return true;
    }

    private static DynValue? ResolveTableFunction(DynValue table, params string[] functionNames)
    {
        if (table.Type != DataType.Table || table.Table is null)
        {
            return null;
        }

        foreach (var functionName in functionNames)
        {
            var function = table.Table.Get(functionName);

            if (function.Type == DataType.Function)
            {
                return function;
            }
        }

        return null;
    }
}
