using MoonSharp.Interpreter;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Coordinates Lua-requested background jobs and marshals completion back to the game loop.
/// </summary>
public interface IAsyncLuaJobService
{
    /// <summary>
    /// Schedules a job without in-flight deduplication.
    /// </summary>
    bool Run(string jobName, string requestId, Table? payload = null);

    /// <summary>
    /// Schedules a keyed job only when the same key is not already in flight.
    /// </summary>
    bool TryRun(string jobName, string key, string requestId, Table? payload = null);
}
