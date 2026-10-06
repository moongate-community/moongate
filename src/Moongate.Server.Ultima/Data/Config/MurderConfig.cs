namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     The murder counts: how long they last, and how a victim reports a kill. The defaults are ModernUO's.
/// </summary>
public sealed class MurderConfig
{
    private const int MaximumHours = 8760;
    private const int MaximumSeconds = 86400;

    /// <summary>
    ///     Gets or sets the hours a short-term murder lasts before it is forgotten.
    /// </summary>
    public int ShortTermHours { get; set; } = 8;

    /// <summary>
    ///     Gets or sets the hours a long-term kill lasts before it is forgotten.
    /// </summary>
    public int LongTermHours { get; set; } = 40;

    /// <summary>
    ///     Gets or sets the seconds after a death before the victim is asked to report who killed it.
    /// </summary>
    public int ReportDelaySeconds { get; set; } = 4;

    /// <summary>
    ///     Gets or sets the minutes during which a victim cannot report the same killer again.
    /// </summary>
    public int RecentlyReportedMinutes { get; set; } = 10;

    /// <summary>
    ///     Gets or sets the seconds an attack on an innocent keeps the attacker reportable by the victim.
    /// </summary>
    public int AggressorSeconds { get; set; } = 120;

    public void Validate()
    {
        Check("short_term_hours", ShortTermHours, MaximumHours);
        Check("long_term_hours", LongTermHours, MaximumHours);
        Check("report_delay_seconds", ReportDelaySeconds, MaximumSeconds);
        Check("recently_reported_minutes", RecentlyReportedMinutes, MaximumSeconds);
        Check("aggressor_seconds", AggressorSeconds, MaximumSeconds);
    }

    private static void Check(string name, int value, int maximum)
    {
        if (value < 1 || value > maximum)
        {
            throw new InvalidOperationException($"ultima.murder.{name} must be from 1 to {maximum}, found {value}.");
        }
    }
}
