namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings of the calendar: the time zone in which the hours of
///     <c>
///         data/schedule.toml
///     </c>
///     are read.
/// </summary>
public sealed class ScheduleConfig
{
    /// <summary>
    ///     Gets or sets the IANA id of the time zone, such as
    ///     <c>
    ///         Europe/Rome
    ///     </c>
    ///     ; empty is the zone of the system.
    /// </summary>
    public string TimeZone { get; set; } = "";

    /// <summary>
    ///     Gets the time zone the settings name.
    /// </summary>
    public TimeZoneInfo Resolve()
    {
        return string.IsNullOrWhiteSpace(TimeZone)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(TimeZone.Trim());
    }

    /// <summary>
    ///     Validates the settings before server services begin startup.
    /// </summary>
    public void Validate()
    {
        try
        {
            Resolve();
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"ultima.schedule.time_zone '{TimeZone}' is not a time zone of this system. On Linux the zones come from the tzdata package.",
                exception
            );
        }
    }
}
