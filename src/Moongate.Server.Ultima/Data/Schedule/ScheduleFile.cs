namespace Moongate.Server.Ultima.Data.Schedule;

/// <summary>
///     <c>
///         data/schedule.toml
///     </c>
///     : the timed tasks and the seasonal events of the shard.
/// </summary>
public sealed class ScheduleFile
{
    /// <summary>
    ///     Gets or sets the
    ///     <c>
    ///         [[task]]
    ///     </c>
    ///     entries.
    /// </summary>
    public List<ScheduleTask> Task { get; set; } = [];

    /// <summary>
    ///     Gets or sets the
    ///     <c>
    ///         [[event]]
    ///     </c>
    ///     entries.
    /// </summary>
    public List<ScheduleEvent> Event { get; set; } = [];
}
