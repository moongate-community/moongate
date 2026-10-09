namespace Moongate.Server.Ultima.Data.Schedule;

/// <summary>
///     One timed task: when it runs and what it does.
/// </summary>
public sealed class ScheduleTask
{
    /// <summary>
    ///     Gets or sets the name of the task, such as
    ///     <c>
    ///         nightly_restart
    ///     </c>
    ///     .
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     Gets or sets when it runs.
    /// </summary>
    public ScheduleWhen When { get; set; } = new();

    /// <summary>
    ///     Gets or sets what it does:
    ///     <c>
    ///         shutdown
    ///     </c>
    ///     ,
    ///     <c>
    ///         broadcast
    ///     </c>
    ///     or
    ///     <c>
    ///         lua
    ///     </c>
    ///     .
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>
    ///     Gets or sets, for a shutdown, the seconds before the stop at which everybody is told; each smaller than the one before.
    /// </summary>
    public List<int> Warnings { get; set; } = [];

    /// <summary>
    ///     Gets or sets, for a broadcast, the id of a localized message.
    /// </summary>
    public int? Message { get; set; }

    /// <summary>
    ///     Gets or sets, for a broadcast, the text to send.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    ///     Gets or sets, for a Lua task, the script under
    ///     <c>
    ///         scripts/events
    ///     </c>
    ///     , without the extension.
    /// </summary>
    public string? Script { get; set; }

    /// <summary>
    ///     Gets or sets, for a Lua task, the function to call;
    ///     <c>
    ///         run
    ///     </c>
    ///     when empty.
    /// </summary>
    public string? Function { get; set; }
}
