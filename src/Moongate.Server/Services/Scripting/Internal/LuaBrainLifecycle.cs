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
            state.OnDeathFunction = null;
            state.OnSpawnFunction = null;
            state.OnInRangeFunction = null;
            state.OnOutRangeFunction = null;
            state.OnGetContextMenusFunction = null;
            state.OnSelectedContextMenuFunction = null;
            state.IsFaulted = true;

            return;
        }

        state.OnSpeechFunction = hooks.OnSpeechFunction;
        state.OnDeathFunction = hooks.OnDeathFunction;
        state.OnSpawnFunction = hooks.OnSpawnFunction;
        state.OnInRangeFunction = hooks.OnInRangeFunction;
        state.OnOutRangeFunction = hooks.OnOutRangeFunction;
        state.OnGetContextMenusFunction = hooks.OnGetContextMenusFunction;
        state.OnSelectedContextMenuFunction = hooks.OnSelectedContextMenuFunction;
        state.OnEventFunction = hooks.OnEventFunction;

        var brainLoop = hooks.BrainLoopFunction;

        if (brainLoop is null || brainLoop.Type != DataType.Function)
        {
            logger.Warning(
                "Lua brain table {BrainTable} for mobile {MobileId} does not expose brain_loop.",
                state.BrainTableName,
                state.MobileId
            );

            state.BrainCoroutine = null;
            state.IsFaulted = true;

            return;
        }

        state.BrainCoroutine = luaScript.CreateCoroutine(brainLoop).Coroutine;
    }
}
