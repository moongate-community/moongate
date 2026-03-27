using Moongate.Server.Data.Internal.Scripting;
using MoonSharp.Interpreter;
using Serilog;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Handles binding and runtime initialization of brain hooks.
/// </summary>
internal static class LuaBrainLifecycle
{
    public static void InitializeRuntimeState(Script? luaScript, LuaBrainRuntimeState state, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(logger);

        if (luaScript is null)
        {
            return;
        }

        if (!LuaBrainHookBinder.TryBind(luaScript, state.BrainTableName, out var hooks))
        {
            logger.Warning(
                "Lua brain table {BrainTable} not found for mobile {MobileId}.",
                state.BrainTableName,
                state.MobileId
            );
            state.BrainCoroutine = null;
            state.OnEventFunction = null;
            state.OnSpeechFunction = null;
            state.OnBeforeDeathFunction = null;
            state.OnDeathFunction = null;
            state.OnAfterDeathFunction = null;
            state.OnSpawnFunction = null;
            state.OnAttackFunction = null;
            state.OnMissedAttackFunction = null;
            state.OnAttackedFunction = null;
            state.OnMissedByAttackFunction = null;
            state.OnInRangeFunction = null;
            state.OnOutRangeFunction = null;
            state.OnGetContextMenusFunction = null;
            state.OnSelectedContextMenuFunction = null;
            state.IsFaulted = true;

            return;
        }

        state.OnSpeechFunction = hooks.OnSpeechFunction;
        state.OnBeforeDeathFunction = hooks.OnBeforeDeathFunction;
        state.OnDeathFunction = hooks.OnDeathFunction;
        state.OnAfterDeathFunction = hooks.OnAfterDeathFunction;
        state.OnSpawnFunction = hooks.OnSpawnFunction;
        state.OnAttackFunction = hooks.OnAttackFunction;
        state.OnMissedAttackFunction = hooks.OnMissedAttackFunction;
        state.OnAttackedFunction = hooks.OnAttackedFunction;
        state.OnMissedByAttackFunction = hooks.OnMissedByAttackFunction;
        state.OnInRangeFunction = hooks.OnInRangeFunction;
        state.OnOutRangeFunction = hooks.OnOutRangeFunction;
        state.OnGetContextMenusFunction = hooks.OnGetContextMenusFunction;
        state.OnSelectedContextMenuFunction = hooks.OnSelectedContextMenuFunction;
        state.OnEventFunction = hooks.OnEventFunction;

        var onThink = hooks.OnThinkFunction;

        if (onThink is null || onThink.Type != DataType.Function)
        {
            logger.Warning(
                "Lua brain table {BrainTable} for mobile {MobileId} does not expose on_think.",
                state.BrainTableName,
                state.MobileId
            );

            state.BrainCoroutine = null;
            state.IsFaulted = true;

            return;
        }

        state.BrainCoroutine = luaScript.CreateCoroutine(onThink).Coroutine;
    }
}
