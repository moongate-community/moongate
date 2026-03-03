using Moongate.Scripting.Attributes.Scripts;

namespace Moongate.Server.Modules;

[ScriptModule("time", "Provides time helpers for scripts.")]

/// <summary>
/// Exposes monotonic time helpers to Lua scripts.
/// </summary>
public sealed class TimeModule
{
    [ScriptFunction("now_ms", "Returns monotonic milliseconds since process start.")]
    public long NowMs()
        => Environment.TickCount64;
}
