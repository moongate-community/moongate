using Moongate.Server.Data.Internal.Scripting;
using Moongate.UO.Data.Ids;
using MoonSharp.Interpreter;

namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Executes one MoonSharp brain tick for a runtime state.
/// </summary>
internal static class LuaBrainTickExecutor
{
    public static long Tick(
        long nowMilliseconds,
        Script luaScript,
        LuaBrainRuntimeState state,
        int defaultTickMilliseconds,
        int faultRetryMilliseconds,
        Action<Serial> unregister
    )
    {
        ArgumentNullException.ThrowIfNull(luaScript);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(unregister);

        if (state.BrainCoroutine is null)
        {
            state.IsFaulted = true;

            return LuaBrainFaultPolicy.NextWakeAfterFault(nowMilliseconds, faultRetryMilliseconds);
        }

        LuaBrainPendingEventDispatcher.DispatchAll(luaScript, state);

        if (state.BrainCoroutine.State == CoroutineState.Dead)
        {
            unregister(state.MobileId);

            return nowMilliseconds + defaultTickMilliseconds;
        }

        var result = state.BrainCoroutine.State == CoroutineState.NotStarted
                         ? state.BrainCoroutine.Resume((uint)state.MobileId)
                         : state.BrainCoroutine.Resume();
        var delay = ParseYieldDelay(result, defaultTickMilliseconds);

        if (state.BrainCoroutine.State == CoroutineState.Dead)
        {
            unregister(state.MobileId);
        }

        return nowMilliseconds + delay;
    }

    private static int ParseYieldDelay(DynValue yielded, int defaultTickMilliseconds)
    {
        if (yielded.Type == DataType.Number)
        {
            var value = (int)Math.Round(yielded.Number);

            return value <= 0 ? defaultTickMilliseconds : value;
        }

        return defaultTickMilliseconds;
    }
}
