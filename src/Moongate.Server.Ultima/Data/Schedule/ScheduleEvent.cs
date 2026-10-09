namespace Moongate.Server.Ultima.Data.Schedule;

/// <summary>
///     A seasonal event: a window of dates, repeated every year.
/// </summary>
public sealed class ScheduleEvent
{
    /// <summary>
    ///     Gets or sets the name scripts know it by, such as
    ///     <c>
    ///         halloween
    ///     </c>
    ///     .
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     Gets or sets the name shown to the staff.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Gets or sets the first day,
    ///     <c>
    ///         MM-dd
    ///     </c>
    ///     .
    /// </summary>
    public string From { get; set; } = "";

    /// <summary>
    ///     Gets or sets the last day,
    ///     <c>
    ///         MM-dd
    ///     </c>
    ///     , inclusive; the window may cross the new year.
    /// </summary>
    public string To { get; set; } = "";
}
