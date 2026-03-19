using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Loads, schedules and executes Lua-authored scheduled events.
/// </summary>
public interface IScheduledEventService : IMoongateService
{
    /// <summary>
    /// Gets the number of scheduled event definitions loaded into the runtime.
    /// </summary>
    int GetScheduledEventCount();
}
