namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for crimes: how long a mobile stays a criminal, and the guards that are called on one.
/// </summary>
public sealed class CrimeConfig
{
    private const int MaximumSeconds = 86400;

    /// <summary>
    ///     Gets or sets the seconds a mobile stays a criminal after its last criminal act; 120 is ModernUO's and UOX3's.
    /// </summary>
    public int CriminalSeconds { get; set; } = 120;

    /// <summary>
    ///     Gets or sets whether a player that says "guards" in a guarded region brings a guard onto the criminals near
    ///     it.
    /// </summary>
    public bool GuardsEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the mobile template of a guard that is called; <c>guard</c> is UOX3's list of the town guards.
    /// </summary>
    public string GuardTemplate { get; set; } = "guard";

    /// <summary>
    ///     Gets or sets the mobile template of a guard that is called in Ilshenar and Malas, as ModernUO's archer guards.
    /// </summary>
    public string ArcherGuardTemplate { get; set; } = "archerguard";

    /// <summary>
    ///     Gets or sets the seconds a called guard stays before it leaves; 40 is about ModernUO's.
    /// </summary>
    public int GuardSeconds { get; set; } = 40;

    /// <summary>
    ///     Validates the section before server services begin startup: the times from a second to a day, a template
    ///     named.
    /// </summary>
    public void Validate()
    {
        if (GuardSeconds is < 1 or > MaximumSeconds)
        {
            throw new InvalidOperationException(
                $"ultima.crime.guard_seconds must be from 1 to {MaximumSeconds}, found {GuardSeconds}."
            );
        }

        if (string.IsNullOrWhiteSpace(ArcherGuardTemplate))
        {
            throw new InvalidOperationException("ultima.crime.archer_guard_template cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(GuardTemplate))
        {
            throw new InvalidOperationException("ultima.crime.guard_template cannot be empty.");
        }

        if (CriminalSeconds is < 1 or > MaximumSeconds)
        {
            throw new InvalidOperationException(
                $"ultima.crime.criminal_seconds must be from 1 to {MaximumSeconds}, found {CriminalSeconds}."
            );
        }
    }
}
