namespace Moongate.Server.Ultima.Data.Config;

/// <summary>
///     TOML settings for crimes: how long a mobile stays a criminal.
/// </summary>
public sealed class CrimeConfig
{
    private const int MaximumSeconds = 86400;

    /// <summary>
    ///     Gets or sets the seconds a mobile stays a criminal after its last criminal act; 120 is ModernUO's and UOX3's.
    /// </summary>
    public int CriminalSeconds { get; set; } = 120;

    /// <summary>
    ///     Validates the section before server services begin startup: from a second to a day.
    /// </summary>
    public void Validate()
    {
        if (CriminalSeconds is < 1 or > MaximumSeconds)
        {
            throw new InvalidOperationException(
                $"ultima.crime.criminal_seconds must be from 1 to {MaximumSeconds}, found {CriminalSeconds}."
            );
        }
    }
}
