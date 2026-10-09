using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Schedule;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The calendar of timed tasks declared in
///     <c>
///         data/schedule.toml
///     </c>
///     : shutdowns with warnings, messages to everybody and calls to Lua functions.
/// </summary>
public interface IScheduleService : IMoongateStartupService
{
    /// <summary>
    ///     Gets the tasks of the calendar.
    /// </summary>
    IReadOnlyList<ScheduleTask> Tasks { get; }

    /// <summary>
    ///     Gets when a task runs next: the stop, for a shutdown.
    /// </summary>
    /// <param name="taskId">
    ///     The id of the task.
    /// </param>
    /// <returns>
    ///     The next occurrence after now, or null for an unknown id.
    /// </returns>
    DateTimeOffset? NextOccurrence(string taskId);
}
