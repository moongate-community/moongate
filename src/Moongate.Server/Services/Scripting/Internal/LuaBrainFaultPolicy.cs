namespace Moongate.Server.Services.Scripting.Internal;

/// <summary>
/// Computes fault backoff timing for Lua brain runtime.
/// </summary>
internal static class LuaBrainFaultPolicy
{
    public static long NextWakeAfterFault(long nowMilliseconds, int faultRetryMilliseconds)
        => nowMilliseconds + faultRetryMilliseconds;
}
