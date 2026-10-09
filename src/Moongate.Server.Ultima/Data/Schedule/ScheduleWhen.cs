namespace Moongate.Server.Ultima.Data.Schedule;

/// <summary>
///     The rule of a task: every hour, day or week, at a time.
/// </summary>
public sealed class ScheduleWhen
{
    /// <summary>
    ///     Gets or sets
    ///     <c>
    ///         hour
    ///     </c>
    ///     ,
    ///     <c>
    ///         day
    ///     </c>
    ///     or
    ///     <c>
    ///         week
    ///     </c>
    ///     .
    /// </summary>
    public string Every { get; set; } = "";

    /// <summary>
    ///     Gets or sets
    ///     <c>
    ///         HH:MM
    ///     </c>
    ///     (24 hours), or
    ///     <c>
    ///         :MM
    ///     </c>
    ///     for every hour.
    /// </summary>
    public string At { get; set; } = "";

    /// <summary>
    ///     Gets or sets, for every week, the days (
    ///     <c>
    ///         mon
    ///     </c>
    ///     to
    ///     <c>
    ///         sun
    ///     </c>
    ///     ); every day when empty.
    /// </summary>
    public List<string> Days { get; set; } = [];
}
